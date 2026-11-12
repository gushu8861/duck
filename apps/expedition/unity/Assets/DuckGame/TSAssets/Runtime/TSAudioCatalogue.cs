// Populated by the editor installer (TSAudioWiring). Do not edit by hand.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.TS
{
    /// <summary>
    /// Named lookup over every audio clip imported from Trials Survivors.
    ///
    /// IMPORTANT — why this class has a serialized table:
    /// The auto-generated predecessor exposed Register()/Play() but nothing ever called
    /// Register(), so the runtime dictionary stayed empty and all 165 imported clips were
    /// dead weight: every Play() returned silently. The table below is filled by an editor
    /// pass (TSAudioWiring) with real clip references.
    ///
    /// Heavy clips are kept OUT of memory until first use: the importer marks them
    /// "preload audio data = false", and Get() forces the load on demand.
    /// </summary>
    public sealed class TSAudioCatalogue : MonoBehaviour
    {
        [Serializable]
        public struct KeyedClip
        {
            public string key;
            public AudioClip clip;
        }

        public static TSAudioCatalogue Instance { get; private set; }

        [SerializeField] KeyedClip[] clips = Array.Empty<KeyedClip>();

        readonly Dictionary<string, AudioClip> map = new Dictionary<string, AudioClip>();
        public IReadOnlyDictionary<string, AudioClip> All => map;
        public AudioSource Source { get; private set; }

        public int TableCount => clips != null ? clips.Length : 0;
        public int LoadedCount => map.Count;
        public int PlayedCount { get; private set; }
        public int MissingPlays { get; private set; }
        public string LastMissingKey { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Source = gameObject.GetComponent<AudioSource>();
            if (Source == null) Source = gameObject.AddComponent<AudioSource>();
            Source.playOnAwake = false;
            Source.spatialBlend = 0f;

            BuildMap();
        }

        void BuildMap()
        {
            map.Clear();
            if (clips == null) return;
            for (int i = 0; i < clips.Length; i++)
            {
                var e = clips[i];
                if (string.IsNullOrEmpty(e.key) || e.clip == null) continue;
                map[e.key] = e.clip;
            }
        }

        /// <summary>Editor-time entry point used by the wiring pass.</summary>
        public void SetTable(KeyedClip[] table)
        {
            clips = table;
            BuildMap();
        }

        public void Register(string key, AudioClip clip)
        {
            if (!string.IsNullOrEmpty(key) && clip != null) map[key] = clip;
        }

        public AudioClip Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (!map.TryGetValue(key, out var c)) return null;
            // Non-preloaded clips are not resident yet; LoadAudioData makes them playable.
            if (c != null && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();
            return c;
        }

        public void Play(string key, float volume = 1f)
        {
            var c = Get(key);
            if (c == null)
            {
                MissingPlays++;
                LastMissingKey = key;
                return;
            }
            PlayedCount++;
            Source.PlayOneShot(c, volume);
        }

        /// <summary>Play a random clip from a named family so repeats do not sound mechanical.</summary>
        public void PlayFamily(string[] family, float volume = 1f)
        {
            if (family == null || family.Length == 0) return;
            Play(family[UnityEngine.Random.Range(0, family.Length)], volume);
        }

        public void PlayMusic(string key, float volume = .6f)
        {
            var c = Get(key);
            if (c == null) { MissingPlays++; LastMissingKey = key; return; }
            Source.clip = c;
            Source.loop = true;
            Source.volume = volume;
            Source.Play();
        }

        public bool Has(string key) => !string.IsNullOrEmpty(key) && map.ContainsKey(key);
    }
}
