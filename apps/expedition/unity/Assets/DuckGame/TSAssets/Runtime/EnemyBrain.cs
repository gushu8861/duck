using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DuckGame.TS
{
    /// <summary>
    /// Enemy archetypes and their movement brains, modelled on the reference project's
    /// AIMovementBehaviour_Chase / _RangeKeeper / _FrogFlee split. Three roles cover the
    /// design doc's requirement that silhouette and behaviour read before raw stats:
    ///   Chaser   - always closes in, creates movement pressure
    ///   Keeper   - holds a preferred distance, forces route and cover decisions
    ///   Fleer    - backs off when the player is close, punishes over-commitment
    /// </summary>
    public enum EnemyRole { Chaser, Keeper, Fleer }

    [Serializable]
    public struct EnemyTuning
    {
        public EnemyRole role;
        public float moveSpeed;
        public float preferredRange;   // Keeper/Fleer only
        public float aggression;       // 0..1, scales how hard it commits
        public float maxHealth;
        public float contactDamage;
    }

    /// <summary>
    /// Simple, readable enemy brain. Deliberately small: one behaviour switch, no FSM
    /// library, so the movement intent is obvious from the code and easy to tune.
    ///
    /// Implements IOrbHitReceiver so the project's EXISTING orb pipeline damages it —
    /// no changes needed to OrbShooter or OrbProjectile.
    /// </summary>
    public sealed class EnemyBrain : MonoBehaviour, DuckGame.IOrbHitReceiver
    {
        public EnemyTuning tuning = new EnemyTuning
        {
            role = EnemyRole.Chaser,
            moveSpeed = 3.2f,
            preferredRange = 9f,
            aggression = .75f,
            maxHealth = 30f,
            contactDamage = 8f,
        };

        public Transform target;

        [Header("Visual feedback")]
        public Transform body;
        public Renderer bodyRenderer;
        public Color idleTint = new Color(.78f, .28f, .22f);
        public Color chargeTint = new Color(1f, .55f, .15f);

        [Header("Ranged attack")]
        [Tooltip("Shots only fire on this interval; 0 disables ranged attacks.")]
        public float shotInterval = 2.4f;
        public float shotDamage = 7f;
        public float shotSpeed = 16f;
        [Tooltip("Only shoot within this distance.")]
        public float shotRange = 16f;

        float nextShotTime;
        ProjectilePool shotPool;
        int shotSpec = -1;

        /// <summary>How many ranged shots this enemy has fired (diagnostics/verification).</summary>
        public int ShotsFired { get; private set; }
        /// <summary>The pool spec index used for this enemy's shots, or -1.</summary>
        public int SpecForTest => shotSpec;
        /// <summary>Shot speed override used by verification; 0 keeps the inspector value.</summary>
        public float speedForTest;

        Vector3 home;
        float health;
        float bobPhase;

        public float Health => health;
        public bool IsDead => health <= 0f;

        void Awake()
        {
            home = transform.position;
            health = tuning.maxHealth;
            bobPhase = UnityEngine.Random.value * 6.283f;
            if (body == null) body = transform;

            // Every hostile needs a hit volume. The downloaded creature prefabs ship without
            // colliders, so without this the player can shoot and nothing ever registers.
            EnsureHitVolume();
        }

        /// <summary>
        /// Adds one capsule fitted to the renderer bounds if the prefab has no USABLE hit
        /// volume.
        ///
        /// Three details matter:
        ///   - A disabled CharacterController still counts as "a collider exists", which made an
        ///     earlier version skip the hit volume entirely on enemies spawned by IslandSpawner
        ///     (it disables the controller before adding this brain). Only enabled, non-trigger
        ///     colliders count as a usable hit volume.
        ///   - The capsule is a TRIGGER. Enemies are moved by writing transform.position, so a
        ///     solid collider would block the player's CharacterController and shove the player
        ///     around. A trigger never blocks CharacterController motion, yet still answers the
        ///     orb sweep, which queries QueryTriggerInteraction.Collide.
        ///   - A kinematic Rigidbody accompanies it. Without one, shifting transform.position
        ///     every frame forces PhysX to rebuild the collider's static AABB each tick.
        /// </summary>
        public void EnsureHitVolume()
        {
            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                return;   // a real, usable volume already exists
            }

            var rs = GetComponentsInChildren<Renderer>(true);
            Bounds b;
            if (rs.Length == 0) b = new Bounds(transform.position, Vector3.one);
            else
            {
                b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            }

            float worldHeight = Mathf.Max(.2f, b.size.y);
            float worldRadius = Mathf.Max(.12f, Mathf.Max(b.size.x, b.size.z) * .5f);

            // Collider dimensions are LOCAL and get multiplied by the transform scale, and the
            // imported creatures are rescaled to ~1.6 m. Divide back out or the hit volume
            // ends up many times too large.
            Vector3 s = transform.lossyScale;
            float scaleY = Mathf.Max(.0001f, Mathf.Abs(s.y));
            float scaleXZ = Mathf.Max(.0001f, Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)));

            float localRadius = worldRadius / scaleXZ;
            float localHeight = Mathf.Max(worldHeight / scaleY, localRadius * 2f);

            var cap = gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1;                       // Y axis
            cap.radius = localRadius;
            cap.height = localHeight;
            cap.isTrigger = true;

            // Deliberately NO Rigidbody here.
            //
            // An earlier version added a kinematic Rigidbody to avoid per-frame static-AABB
            // rebuilds. That broke movement outright: this brain moves enemies by writing
            // transform.position, and with a Rigidbody owning the transform the physics step
            // snapped every enemy back to where the body was created. The visible symptom was
            // every enemy standing on the player at distance 0.
            //
            // The real fix for the AABB cost is Physics.SyncTransforms once per frame after all
            // enemies have moved (see WaveController.Update) — the project runs with
            // m_AutoSyncTransforms = 0, so transform writes are not pushed to PhysX otherwise.

            // Capsule collider centre is in local space; convert the world centre offset.
            Vector3 local = transform.InverseTransformPoint(b.center);
            cap.center = new Vector3(0f, local.y, 0f);
        }

        /// <summary>
        /// Layer used for enemy hit volumes. Kept as a documented constant even though the
        /// trigger approach no longer needs a dedicated layer: if a future enemy prefab ships a
        /// SOLID body collider, moving it here and clearing that bit in the player's
        /// CharacterController layer mask is the fix.
        /// </summary>
        public const string EnemyHitboxLayer = "EnemyHitbox";

        static void SetLayerDeep(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerDeep(t.GetChild(i), layer);
        }

        void Start()
        {
            // Join the current island's wave so the clear rule counts this enemy. Enemies
            // placed straight into the scene used to be invisible to WaveController, which
            // made an untouched island read as already cleared.
            var wave = DuckGame.Expedition.WaveController.Instance;
            if (wave != null) wave.Register(this);
        }

        void Update()
        {
            if (IsDead || target == null) return;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist < .0001f) return;

            Vector3 dir = toTarget / dist;
            Vector3 wish = Vector3.zero;

            switch (tuning.role)
            {
                case EnemyRole.Chaser:
                    // Always close in; aggression only affects the final approach speed.
                    wish = dir;
                    break;

                case EnemyRole.Keeper:
                    // Hold a band around preferredRange: back off if too close, close if too far.
                    if (dist > tuning.preferredRange * 1.15f) wish = dir;
                    else if (dist < tuning.preferredRange * .85f) wish = -dir;
                    else wish = Vector3.Cross(Vector3.up, dir) * .35f;  // strafe
                    break;

                case EnemyRole.Fleer:
                    // Retreat when the player is inside the comfort zone, otherwise idle toward home.
                    wish = (dist < tuning.preferredRange) ? -dir : (home - transform.position).normalized * .5f;
                    break;
            }

            float speed = tuning.moveSpeed * Mathf.Lerp(.6f, 1f, tuning.aggression);
            transform.position += wish.normalized * speed * Time.deltaTime;

            // Face the player so the silhouette stays readable.
            if (dir.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up), Time.deltaTime * 8f);

            // Subtle bob sells the "alive" read without needing an animation clip.
            bobPhase += Time.deltaTime * 5f;
            if (body != null)
                body.localPosition = new Vector3(0f, Mathf.Sin(bobPhase) * .06f, 0f);

            // Tint shifts as it closes in: at-a-glance danger cue.
            if (bodyRenderer != null)
            {
                float closeness = 1f - Mathf.Clamp01((dist - 2f) / 10f);
                var mat = bodyRenderer.sharedMaterial;
                if (mat != null) mat.color = Color.Lerp(idleTint, chargeTint, closeness * closeness);
            }

            TryShoot(dist);
        }

        /// <summary>
        /// Ranged pressure for the two roles that are supposed to threaten from range.
        ///
        /// A Chaser stays purely melee on purpose: the design doc wants the three archetypes to
        /// read differently, and giving every enemy a gun would flatten that. Keeper and Fleer
        /// are the ones that hang back, so they are the ones that shoot.
        /// </summary>
        void TryShoot(float dist)
        {
            if (shotInterval <= 0f || IsDead || target == null) return;
            if (tuning.role == EnemyRole.Chaser) return;
            if (dist > shotRange) return;
            if (Time.time < nextShotTime) return;

            EnsureShotPool();
            if (shotPool == null) return;

            nextShotTime = Time.time + shotInterval * UnityEngine.Random.Range(.85f, 1.2f);

            Vector3 muzzle = transform.position + Vector3.up * 1.0f;
            Vector3 aim = target.position + Vector3.up * 1.1f;
            Vector3 dir = (aim - muzzle).normalized;

            // Do not fire through terrain: an enemy behind a wall would otherwise land free
            // damage the player cannot see coming. The shot is simply skipped this interval.
            if (Physics.Raycast(muzzle, dir, out var blocked, Vector3.Distance(muzzle, aim),
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var hitPlayer = blocked.collider.GetComponentInParent<DuckGame.IEnemyShotReceiver>();
                if (hitPlayer == null) return;
            }

            shotPool.Spawn(shotSpec, muzzle, dir);
            ShotsFired++;
        }

        void EnsureShotPool()
        {
            if (shotPool != null && shotSpec >= 0) return;

            // Reuse the scene pool so enemy and player projectiles share one budget and one
            // sweep implementation. The pool has exactly ONE hit handler and the player's
            // PooledOrbBridge owns it; that handler dispatches by receiver type, so an enemy
            // shot aimed at the player is resolved there. This method therefore only registers
            // the spec and never installs its own handler (doing so used to steal the player's
            // shots and drop their damage).
            shotPool = FindObjectOfType<ProjectilePool>();
            if (shotPool == null) return;

            shotSpec = shotPool.RegisterEnemySpec(new ProjectileSpec
            {
                speed = speedForTest > 0f ? speedForTest : shotSpeed,
                damage = shotDamage,      // read by the bridge's hit dispatch, not by the pool
                lifetime = 4f,
                radius = .16f,
                gravity = 0f,
                scale = Vector3.one * .26f,
                tint = new Color(1f, .35f, .28f),
            });
        }

        public void ApplyDamage(float amount)
        {
            if (IsDead) return;
            health -= amount;
            if (health <= 0f)
            {
                health = 0f;
                // Death is left to the caller so loot/score rules stay in one place.
            }
        }

        /// <summary>Contact damage is only applied when the caller detects actual overlap.</summary>
        public float ContactDamage => tuning.contactDamage;

        /// <summary>
        /// Called by the existing orb pipeline (OrbProjectile.ApplyImpact). Keeping this
        /// here means the shared shooting code needs no modification at all.
        /// </summary>
        public void ReceiveOrbHit(DuckGame.OrbShotContext shot, DuckGame.OrbImpactContext impact)
        {
            if (IsDead) return;
            ApplyDamage(DamagePerHit);
            if (DuckGame.Expedition.CombatBridge.Instance != null)
                DuckGame.Expedition.CombatBridge.Instance.NotifyHit(this, impact.Point);
        }

        /// <summary>Base damage an orb applies; CombatBridge may scale this for upgrades.</summary>
        public static float DamagePerHit = 14f;
    }
}
