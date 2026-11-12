using UnityEngine;
using DuckGame;          // OrbShooter, OrbProjectile, OrbShotContext, IOrbHitReceiver

namespace DuckGame.TS
{
    /// <summary>
    /// Connects the goose's REAL firing path to the pooled projectile system.
    ///
    /// Before this existed, ProjectilePool was instantiated in the hub scene but nothing ever
    /// called Spawn() on it: every shot still created a sphere GameObject + TrailRenderer and
    /// destroyed it on impact. This bridge is the missing wire.
    ///
    /// Flow:
    ///   OrbShooter.Fire()
    ///     -> IOrbShotRouter.TryRoute()            (this class, registered scene-wide)
    ///     -> ProjectilePool.Spawn() per pellet    (fixed pool, no allocation)
    ///     -> pool SphereCast hit
    ///     -> OnHit -> OrbProjectile.ApplyImpact() (same entry point as the legacy path)
    ///     -> IOrbHitReceiver.ReceiveOrbHit()      (EnemyBrain)
    ///     -> CombatBridge.NotifyHit()             (damage numbers, coins, kills, audio)
    ///
    /// Damage, coins, audio and the island-clear rule therefore stay in exactly one place.
    /// </summary>
    [DefaultExecutionOrder(45)]
    public sealed class PooledOrbBridge : MonoBehaviour, IOrbShotRouter
    {
        [Header("Wiring (auto-resolved when left empty)")]
        public OrbShooter shooter;
        public ProjectilePool pool;

        [Header("Visuals")]
        public float visualSize = .30f;
        public float trailTime = .16f;
        public bool useWeaponMaterial = true;

        [Header("Fallbacks (used only when the shooter has no OrbConfig)")]
        public float fallbackSpeed = 32f;
        public float fallbackRadius = .10f;
        public float fallbackLifetime = 4f;
        public Color fallbackTint = new Color(.22f, .85f, 1f);

        public bool Active { get; private set; }
        public int RoutedShots { get; private set; }
        public int RoutedPellets { get; private set; }
        public int EnemyHits { get; private set; }
        public int WorldHits { get; private set; }
        /// <summary>Enemy projectiles that landed on the player.</summary>
        public int EnemyShotHits { get; private set; }
        public int PoolExhausted { get; private set; }

        /// <summary>Per-pellet shot description, addressed by the tag the pool hands back.</summary>
        const int RingSize = 1024;
        readonly OrbShotContext[] ring = new OrbShotContext[RingSize];
        readonly OrbConfig[] shotConfig = new OrbConfig[RingSize];
        int nextTag;

        int specIndex = -1;

        /// <summary>Index of this bridge's spec inside the pool. -1 before the first registration.</summary>
        public int SpecIndex => specIndex;

        /// <summary>The spec this bridge currently feeds the pool (after the last RefreshSpec).</summary>
        public ProjectileSpec CurrentSpec => (pool != null && specIndex >= 0) ? pool.GetSpec(specIndex) : default;

        void OnEnable()
        {
            Resolve();
            OrbRouterRegistry.Set(this);
        }

        void OnDisable()
        {
            OrbRouterRegistry.Clear(this);
            // Do NOT null pool.OnHit: the pool treats a null handler as "consume and destroy",
            // so every in-flight projectile would die on contact with no damage, coin or sound.
            // Clearing the live shots is the honest teardown: nothing is in flight, and a later
            // re-enable starts from a clean pool.
            if (pool != null) pool.ClearAll();
        }

        void Resolve()
        {
            if (shooter == null) shooter = FindObjectOfType<OrbShooter>();
            if (pool == null) pool = FindObjectOfType<ProjectilePool>();
            if (pool == null)
            {
                var host = new GameObject("PooledOrbs");
                host.transform.SetParent(transform, false);
                pool = host.AddComponent<ProjectilePool>();
            }

            pool.trailTime = trailTime;
            pool.visualScale = 1f;
            pool.OnHit = HandleHit;

            if (useWeaponMaterial && shooter != null)
            {
                if (shooter.orbMaterial != null) pool.ApplyBodyMaterial(shooter.orbMaterial);
                // The legacy per-shot projectile used shooter.trailMaterial (a soft fading
                // trail shader), NOT the orb material. Handing the orb material to the
                // TrailRenderer made pooled shots look worse than the path they replaced.
                if (shooter.trailMaterial != null) pool.ApplyTrailMaterial(shooter.trailMaterial);
            }

            EnsureSpec();
        }

        void EnsureSpec()
        {
            var cfg = shooter != null ? shooter.config : null;
            var spec = new ProjectileSpec
            {
                speed = cfg != null ? cfg.speed : fallbackSpeed,
                damage = 1f,        // damage rule lives in EnemyBrain, not in the projectile
                lifetime = cfg != null ? cfg.lifetime : fallbackLifetime,
                radius = cfg != null ? cfg.radius : fallbackRadius,
                gravity = 0f,
                scale = Vector3.one * visualSize,
                tint = cfg != null ? cfg.orbColor : fallbackTint,
            };

            if (specIndex < 0) specIndex = pool.RegisterSpec(spec);
            else pool.UpdateSpec(specIndex, spec);
        }

        /// <summary>Re-read the shooter config, e.g. after a merchant purchase or a reward pick.</summary>
        public void RefreshSpec()
        {
            if (pool == null) return;
            EnsureSpec();
        }

        public bool TryRoute(OrbShooter owner, Vector3 origin, Vector3 direction)
        {
            if (pool == null || owner == null) return false;

            var cfg = owner.config;
            int count = Mathf.Clamp(cfg != null ? cfg.projectileCount : 1, 1, 8);
            float spread = cfg != null ? cfg.spreadDegrees : 0f;

            // Keep the pool's tuning equal to the live weapon, so upgrades take effect on the
            // very next shot instead of needing a reinstall.
            EnsureSpec();

            int routed = 0;
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * .5f) * spread;
                Vector3 dir = Quaternion.AngleAxis(angle, owner.transform.up) * direction;

                int tag = nextTag++ & (RingSize - 1);
                ring[tag] = new OrbShotContext(origin, dir, i, owner.gameObject);
                // Snapshot the config the shot was fired with, exactly like the legacy
                // OrbProjectile did. A later config swap (merchant closes, scene resets) must
                // not change how an in-flight projectile resolves its hit.
                shotConfig[tag] = cfg;

                if (!pool.Spawn(specIndex, origin, dir, tag))
                {
                    PoolExhausted++;
                    break;
                }
                routed++;
            }

            if (routed == 0) return false;

            RoutedShots++;
            RoutedPellets += routed;
            Active = true;

            // Fire sound follows the island's element, so a fire island does not sound like
            // an ice island. Falls back to the generic cast family.
            PlayFireCue();

            return true;
        }

        void PlayFireCue()
        {
            var cat = TSAudioCatalogue.Instance;
            if (cat == null) return;

            string cue = TSSoundMap.ShotDefault;
            var run = DuckGame.Expedition.ExpeditionRun.Instance;
            if (run != null && run.Current != null)
            {
                switch (run.Current.element)
                {
                    case DuckGame.Expedition.EnemyElement.Ice: cue = TSSoundMap.ShotIce; break;
                    case DuckGame.Expedition.EnemyElement.Fire: cue = TSSoundMap.ShotFire; break;
                    case DuckGame.Expedition.EnemyElement.Lightning: cue = TSSoundMap.ShotLightning; break;
                    case DuckGame.Expedition.EnemyElement.Witchcraft: cue = TSSoundMap.ShotWitch; break;
                }
            }

            if (cat.Has(cue)) cat.Play(cue, .45f);
            else cat.PlayFamily(TSSoundMap.Cast, .45f);
        }

        bool HandleHit(ProjectileState state, RaycastHit hit)
        {
            if (hit.collider == null) return true;

            // The pool has ONE handler and both sides use the pool, so dispatch by what was hit:
            //   - IEnemyShotReceiver  -> an enemy projectile landed on the player
            //   - IOrbHitReceiver     -> the player's orb landed on an enemy
            // The spec's damage field is what distinguishes an enemy shot (non-zero) from a
            // player pellet (the bridge registers damage = 1 and lets EnemyBrain own the rule).
            var playerSide = hit.collider.GetComponentInParent<IEnemyShotReceiver>();
            if (playerSide != null)
            {
                var spec = pool.GetSpec(state.specIndex);
                Vector3 dir = state.velocity.sqrMagnitude > .0001f ? state.velocity.normalized : Vector3.forward;
                playerSide.ReceiveEnemyShot(spec.damage, hit.point, dir);
                EnemyShotHits++;
                return true;
            }

            var receiver = hit.collider.GetComponentInParent<IOrbHitReceiver>();
            // Use the exact shot that spawned this pellet when the tag is intact, otherwise
            // fall back to the live velocity so a hit is never dropped for lack of a context.
            var shot = ring[state.tag & (RingSize - 1)];
            if (shot.Owner == null)
            {
                Vector3 dir = state.velocity.sqrMagnitude > .0001f ? state.velocity.normalized : Vector3.forward;
                shot = new OrbShotContext(state.position, dir, 0, shooter != null ? shooter.gameObject : null);
            }

            var impact = new OrbImpactContext(hit.collider, hit.point, hit.normal, true);

            var cfg = shotConfig.Length > (state.tag & (RingSize - 1))
                ? shotConfig[state.tag & (RingSize - 1)]
                : null;

            if (cfg == null)
            {
                // The legacy path kept its own config reference per projectile, so a shot fired
                // before the config was destroyed still resolved. Refusing to consume the
                // projectile (return false) keeps that shot alive instead of silently eating a
                // hit through ApplyImpact's null-config early-out.
                return false;
            }

            // ApplyImpact is the SAME call the legacy sphere path makes, so IOrbHitReceiver,
            // feedback audio, target confirmation and the impact VFX pool behave identically.
            OrbProjectile.ApplyImpact(cfg, shot, impact);

            if (receiver != null) EnemyHits++;
            else
            {
                WorldHits++;
                var cat = TSAudioCatalogue.Instance;
                if (cat != null && cat.Has(TSSoundMap.HitWorld)) cat.Play(TSSoundMap.HitWorld, .25f);
            }

            return true;
        }
    }
}
