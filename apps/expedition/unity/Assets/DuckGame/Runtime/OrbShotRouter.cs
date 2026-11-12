using UnityEngine;

namespace DuckGame
{
    /// <summary>
    /// Optional replacement for the per-shot GameObject path in <see cref="OrbShooter"/>.
    ///
    /// OrbShooter.Fire() asks the router first. A router that returns true owns the shot:
    /// it is responsible for spawning and advancing the projectile, and for delivering hits
    /// through <see cref="OrbProjectile.ApplyImpact"/> so IOrbHitReceiver / CombatBridge keep
    /// working unchanged.
    ///
    /// Returning false means "I did not take this shot" and OrbShooter falls back to its
    /// original spheres. That keeps every legacy scene and verification harness intact.
    /// </summary>
    public interface IOrbShotRouter
    {
        bool TryRoute(OrbShooter shooter, Vector3 origin, Vector3 direction);
    }

    /// <summary>
    /// Scene-wide fallback for <see cref="OrbShooter.shotRouter"/>.
    ///
    /// The hub scene's shooter instance is buried inside the rig prefab, so wiring a router
    /// per instance would mean re-serialising the prefab. Instead the router registers itself
    /// here on enable and the shooter picks it up. Explicit per-shooter assignment still wins.
    /// </summary>
    public static class OrbRouterRegistry
    {
        public static IOrbShotRouter Current { get; private set; }

        public static void Set(IOrbShotRouter router)
        {
            if (router != null) Current = router;
            else if (Current != null) Current = null;
        }

        public static void Clear(IOrbShotRouter router)
        {
            if (ReferenceEquals(Current, router)) Current = null;
        }
    }
}
