using UnityEngine;

namespace DuckGame
{
    /// <summary>
    /// Implemented by anything an ENEMY projectile can hit.
    ///
    /// Player-side only. The player's own orbs keep using IOrbHitReceiver, so the two damage
    /// paths never mix and a player orb can never "hit" the player.
    /// </summary>
    public interface IEnemyShotReceiver
    {
        void ReceiveEnemyShot(float damage, Vector3 point, Vector3 direction);
    }
}
