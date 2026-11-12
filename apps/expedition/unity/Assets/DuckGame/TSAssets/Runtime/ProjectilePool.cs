using System;
using UnityEngine;

namespace DuckGame.TS
{
    /// <summary>
    /// Arcade-style projectile handling inspired by the Trials Survivors architecture
    /// (SSV2_ProjectileData / SSV2_ProjectileInstance). Instead of a MonoBehaviour per
    /// bullet, projectiles live in a fixed pool and are advanced by hand, which keeps
    /// allocation out of the hot path.
    ///
    /// Data is separated from instance, exactly like the reference:
    ///   ProjectileSpec  = immutable tuning (speed, damage, lifetime, radius, colour)
    ///   ProjectileState = per-shot runtime (position, velocity, age, active, tag)
    ///
    /// Cost notes:
    ///   - No per-shot GameObject, no per-shot Material. Colour goes through a
    ///     MaterialPropertyBlock shared by the whole pool.
    ///   - Hit resolution is a SphereCast per live projectile so fast shots cannot tunnel.
    ///   - Counters exist so headless verification can prove the pool actually ran.
    /// </summary>
    public struct ProjectileSpec
    {
        public float speed;
        public float damage;
        public float lifetime;
        public float radius;
        public float gravity;
        public Vector3 scale;
        public Color tint;
    }

    public struct ProjectileState
    {
        public Vector3 position;
        public Vector3 velocity;
        public float age;
        public bool active;
        public int specIndex;
        /// <summary>Caller-owned id; the pool never interprets it, only reports it back.</summary>
        public int tag;
    }

    /// <summary>
    /// Pooled projectile system. Update() advances every live projectile and reports hits
    /// through a callback so gameplay code stays in charge of damage rules.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class ProjectilePool : MonoBehaviour
    {
        public int capacity = 256;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;
        /// <summary>
        /// Enemy projectiles need this instead of hitMask.
        ///
        /// The player's body sits on layer 2 ("Ignore Raycast"), which DefaultRaycastLayers
        /// EXCLUDES by design, so an enemy shot swept with the default mask can never touch the
        /// player. -5 == Everything except layer 2 (Ignore Raycast) and layer 0? No: -5 keeps
        /// layers 0 and 2 because Ignore Raycast is only skipped by
        /// Physics.DefaultRaycastLayers. Using Everything and filtering by receiver is the
        /// simplest correct choice, and the receiver check already ignores anything else.
        /// </summary>
        public LayerMask enemyShotMask = ~0;
        /// <summary>
        /// Enemy hit volumes are triggers, so the sweep MUST include triggers. The legacy
        /// sphere path used QueryTriggerInteraction.Ignore and relied on solid colliders; the
        /// pooled path cannot, or every enemy becomes unhittable.
        /// </summary>
        public QueryTriggerInteraction triggerMode = QueryTriggerInteraction.Collide;
        public Transform visualRoot;
        public MeshRenderer templateRenderer;

        [Tooltip("Shared material for pooled visuals and trails. Falls back to the primitive default.")]
        public Material visualMaterial;
        [Tooltip("Trail material. Defaults to the body material when unset.")]
        public Material trailMaterial;
        [Tooltip("Seconds a trail keeps its points. 0 disables trails.")]
        public float trailTime = .16f;
        [Tooltip("Extra scale multiplier applied to every projectile visual.")]
        public float visualScale = 1f;

        ProjectileSpec[] specs = Array.Empty<ProjectileSpec>();
        int specCount;
        ProjectileState[] states = Array.Empty<ProjectileState>();
        Transform[] visuals = Array.Empty<Transform>();
        Renderer[] surfaces = Array.Empty<Renderer>();
        TrailRenderer[] trails = Array.Empty<TrailRenderer>();
        MaterialPropertyBlock block;

        /// <summary>Called when a projectile hits something. Return true to consume it.</summary>
        public Func<ProjectileState, RaycastHit, bool> OnHit;

        public int LiveCount { get; private set; }
        public int Spawned { get; private set; }
        public int Consumed { get; private set; }
        public int Expired { get; private set; }
        public int SpecCount => specCount;

        bool ready;

        void Awake() { EnsureInit(); }

        /// <summary>
        /// Idempotent initialisation.
        ///
        /// PooledOrbBridge runs at execution order 45 and this pool at 50, so the bridge's
        /// OnEnable can call RegisterSpec() BEFORE this Awake. Previously Awake then rebuilt
        /// the arrays and kept the stale specCount, leaving a zeroed spec that only looked
        /// correct because TryRoute re-pushed it every shot. RegisterSpec/Spawn now call
        /// EnsureInit first, so order no longer matters.
        /// </summary>
        public void EnsureInit()
        {
            if (ready) return;
            ready = true;

            if (capacity < 8) capacity = 8;
            if (specs == null || specs.Length < 8) specs = new ProjectileSpec[8];
            if (states == null || states.Length != capacity) states = new ProjectileState[capacity];
            if (visuals == null || visuals.Length != capacity) visuals = new Transform[capacity];
            if (surfaces == null || surfaces.Length != capacity) surfaces = new Renderer[capacity];
            if (trails == null || trails.Length != capacity) trails = new TrailRenderer[capacity];
            if (block == null) block = new MaterialPropertyBlock();

            if (capacity <= 0) return;

            if (visualRoot == null)
            {
                var go = new GameObject("ProjectileVisuals");
                go.transform.SetParent(transform, false);
                visualRoot = go.transform;
            }

            for (int i = 0; i < capacity; i++)
            {
                GameObject v;
                if (templateRenderer != null)
                {
                    v = Instantiate(templateRenderer.gameObject, visualRoot);
                }
                else
                {
                    v = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    var col = v.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    v.transform.SetParent(visualRoot, false);
                }
                v.name = "proj_" + i;
                v.SetActive(false);
                visuals[i] = v.transform;
                surfaces[i] = v.GetComponent<Renderer>();
                if (surfaces[i] != null && visualMaterial != null) surfaces[i].sharedMaterial = visualMaterial;

                if (trailTime > 0f)
                {
                    var tr = v.GetComponent<TrailRenderer>();
                    if (tr == null) tr = v.AddComponent<TrailRenderer>();
                    tr.time = trailTime;
                    tr.startWidth = .18f;
                    tr.endWidth = 0f;
                    tr.minVertexDistance = .035f;
                    tr.autodestruct = false;
                    tr.emitting = false;
                    tr.receiveShadows = false;
                    tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (trailMaterial != null) tr.sharedMaterial = trailMaterial;
                    else if (surfaces[i] != null) tr.sharedMaterial = surfaces[i].sharedMaterial;
                    trails[i] = tr;
                }
            }
        }

        /// <summary>Swap the look of every pooled projectile body (e.g. to the weapon's orb material).</summary>
        public void ApplyMaterial(Material mat)
        {
            ApplyBodyMaterial(mat);
        }

        /// <summary>
        /// Body material only. The trail keeps its own material: the orb shader and the soft
        /// trail shader are not interchangeable, and assigning the orb material to a
        /// TrailRenderer made pooled shots lose the trail the legacy path had.
        /// </summary>
        public void ApplyBodyMaterial(Material mat)
        {
            if (mat == null) return;
            visualMaterial = mat;
            for (int i = 0; i < surfaces.Length; i++)
                if (surfaces[i] != null) surfaces[i].sharedMaterial = mat;
        }

        /// <summary>Trail material for every pooled projectile.</summary>
        public void ApplyTrailMaterial(Material mat)
        {
            if (mat == null) return;
            trailMaterial = mat;
            for (int i = 0; i < trails.Length; i++)
                if (trails[i] != null) trails[i].sharedMaterial = mat;
        }

        /// <summary>Enemy-shot spec indices are registered here so Spawn can pick the right mask.</summary>
        readonly System.Collections.Generic.HashSet<int> enemySpecs = new System.Collections.Generic.HashSet<int>();

        /// <summary>Register a tuning entry and get its index for Spawn().</summary>
        public int RegisterSpec(ProjectileSpec spec)
        {
            EnsureInit();
            if (specCount >= specs.Length) Array.Resize(ref specs, specs.Length + 4);
            specs[specCount] = spec;
            return specCount++;
        }

        /// <summary>
        /// Register a spec that belongs to an ENEMY projectile.
        ///
        /// Enemy shots are swept with enemyShotMask instead of hitMask, because the player's
        /// collider lives on layer 2 (Ignore Raycast) which DefaultRaycastLayers excludes.
        /// Without this flag an enemy shot physically cannot reach the player.
        /// </summary>
        public int RegisterEnemySpec(ProjectileSpec spec)
        {
            int i = RegisterSpec(spec);
            enemySpecs.Add(i);
            return i;
        }

        public bool IsEnemySpec(int index) => enemySpecs.Contains(index);

        /// <summary>Replace the tuning of an existing entry (cheap way to retune on upgrade).</summary>
        public void UpdateSpec(int index, ProjectileSpec spec)
        {
            if (index < 0 || index >= specCount) return;
            specs[index] = spec;
        }

        public ProjectileSpec GetSpec(int index)
        {
            if (index < 0 || index >= specCount) return default;
            return specs[index];
        }

        public bool Spawn(int specIndex, Vector3 origin, Vector3 direction) => Spawn(specIndex, origin, direction, 0);

        public bool Spawn(int specIndex, Vector3 origin, Vector3 direction, int tag)
        {
            EnsureInit();
            if (specIndex < 0 || specIndex >= specCount) return false;
            var spec = specs[specIndex];
            bool enemyShot = enemySpecs.Contains(specIndex);

            for (int i = 0; i < capacity; i++)
            {
                if (states[i].active) continue;
                states[i] = new ProjectileState
                {
                    position = origin,
                    velocity = direction.normalized * spec.speed,
                    age = 0f,
                    active = true,
                    specIndex = specIndex,
                    tag = tag,
                };

                var v = visuals[i];
                v.gameObject.SetActive(true);
                v.position = origin;
                v.localScale = (spec.scale == Vector3.zero ? Vector3.one * spec.radius * 2f : spec.scale) * visualScale;

                var r = surfaces[i];
                if (r != null)
                {
                    block.SetColor("_BaseColor", spec.tint);
                    block.SetColor("_Color", spec.tint);
                    block.SetColor("_EmissionColor", spec.tint * 2f);
                    r.SetPropertyBlock(block);
                }

                var tr = trails[i];
                if (tr != null)
                {
                    tr.Clear();
                    tr.startColor = spec.tint;
                    tr.endColor = new Color(spec.tint.r, spec.tint.g, spec.tint.b, 0f);
                    tr.emitting = true;
                }

                LiveCount++;
                Spawned++;
                return true;
            }
            return false;   // pool exhausted
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < capacity; i++)
            {
                if (!states[i].active) continue;
                ref var s = ref states[i];
                var spec = specs[s.specIndex];

                s.age += dt;
                if (s.age >= spec.lifetime || spec.lifetime <= 0f)
                {
                    Despawn(ref s, i);
                    Expired++;
                    continue;
                }

                if (spec.gravity != 0f) s.velocity += Vector3.down * spec.gravity * dt;

                Vector3 step = s.velocity * dt;
                float dist = step.magnitude;
                if (dist > 0f)
                {
                    // Enemy shots use a mask that includes the player's layer; player shots use
                    // the scene mask so they never "hit" the shooter.
                    bool enemy = enemySpecs.Contains(s.specIndex);
                    LayerMask mask = enemy ? enemyShotMask : hitMask;

                    // Sweep so fast projectiles cannot tunnel through thin geometry.
                    if (Physics.SphereCast(s.position, spec.radius, step / dist,
                            out var hit, dist, mask, triggerMode))
                    {
                        // Copy before the callback: it may despawn this slot.
                        var snapshot = s;
                        bool consumed = OnHit == null || OnHit(snapshot, hit);
                        if (consumed)
                        {
                            Despawn(ref s, i);
                            Consumed++;
                            continue;
                        }
                    }
                }

                s.position += step;
                visuals[i].position = s.position;
            }
        }

        void Despawn(ref ProjectileState s, int index)
        {
            s.active = false;
            visuals[index].gameObject.SetActive(false);
            if (trails[index] != null) trails[index].emitting = false;
            if (LiveCount > 0) LiveCount--;
        }

        /// <summary>Kill every live shot (used on island change / scene reset).</summary>
        public void ClearAll()
        {
            for (int i = 0; i < capacity; i++)
            {
                if (!states[i].active) continue;
                Despawn(ref states[i], i);
            }
        }

        void OnDisable()
        {
            // A disabled pool must not leave live projectiles around: they would keep moving
            // with no handler and then be reused as free slots.
            ClearAll();
        }
    }
}
