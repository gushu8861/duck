using System;
using System.Collections.Generic;
using UnityEngine;
using DuckGame.TS;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Player health and enemy contact damage.
    ///
    /// The design doc wants pressure from movement, not from invisible hits, so every source
    /// of damage here goes through a single Damage() call with a readable cause, and there is
    /// a short invulnerability window after each hit to prevent instant drain from a swarm.
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour, DuckGame.IEnemyShotReceiver
    {
        public static PlayerHealth Instance { get; private set; }

        [Header("Health")]
        public float maxHealth = 100f;
        public float regenPerSecond = 0f;

        [Header("Contact")]
        public float contactCheckRadius = .55f;
        public float invulnerableAfterHit = .8f;

        public float Health { get; private set; }
        public bool IsDead => Health <= 0f;

        float sinceHit;
        float lastContactTick;

        public event Action<float, float> OnHealthChanged;   // (current, max)
        public event Action OnDied;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Health = maxHealth;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            sinceHit += Time.deltaTime;

            if (regenPerSecond > 0f && !IsDead && Health < maxHealth)
            {
                Health = Mathf.Min(maxHealth, Health + regenPerSecond * Time.deltaTime);
                OnHealthChanged?.Invoke(Health, maxHealth);
            }

            // Contact damage: poll nearby enemies rather than relying on trigger callbacks,
            // because the enemy prefabs come from an import and may lack a trigger collider.
            if (IsDead || sinceHit < invulnerableAfterHit) return;

            var brains = UnityEngine.Object.FindObjectsOfType<EnemyBrain>();
            float closest = float.MaxValue;
            EnemyBrain source = null;
            foreach (var b in brains)
            {
                if (b == null || b.IsDead) continue;
                float d = Vector3.Distance(b.transform.position, transform.position);
                if (d < contactCheckRadius + .6f && d < closest) { closest = d; source = b; }
            }

            if (source != null)
                Damage(source.ContactDamage, source.transform.position);
        }

        public void Damage(float amount, Vector3 from)
        {
            if (IsDead || amount <= 0f) return;
            if (sinceHit < invulnerableAfterHit) return;

            sinceHit = 0f;
            Health = Mathf.Max(0f, Health - amount);
            OnHealthChanged?.Invoke(Health, maxHealth);

            var cat = DuckGame.TS.TSAudioCatalogue.Instance;
            if (cat != null && cat.Has(DuckGame.TS.TSSoundMap.PlayerHurt))
                cat.Play(DuckGame.TS.TSSoundMap.PlayerHurt, .55f);

            if (Health <= 0f)
            {
                OnDied?.Invoke();
                if (cat != null && cat.Has(DuckGame.TS.TSSoundMap.MusicDefeat))
                    cat.PlayMusic(DuckGame.TS.TSSoundMap.MusicDefeat, .5f);
                if (ExpeditionResult.Instance != null)
                    ExpeditionResult.Instance.SendMessage("Snapshot", SendMessageOptions.DontRequireReceiver);
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Health = Mathf.Min(maxHealth, Health + amount);
            OnHealthChanged?.Invoke(Health, maxHealth);
        }

        public void ResetHealth()
        {
            Health = maxHealth;
            sinceHit = 0f;
            OnHealthChanged?.Invoke(Health, maxHealth);
        }

        /// <summary>
        /// Enemy projectile impact. Uses the same Damage() gate as contact damage, so the
        /// invulnerability window and the death path stay in one place.
        /// </summary>
        public void ReceiveEnemyShot(float damage, Vector3 point, Vector3 direction)
        {
            if (IsDead) return;
            Damage(damage, point);
        }
    }
}
