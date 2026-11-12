using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Deterministic sub-seed derivation, exactly as the expedition design doc specifies:
    ///   sub = Hash(runSeed, islandId, channel, generatorVersion)
    /// Never use UnityEngine.Random or GetHashCode() here — both vary per process and would
    /// break replay of a fixed seed. FNV-1a over the packed inputs is stable everywhere.
    /// </summary>
    public static class SeedDerivation
    {
        public const int GeneratorVersion = 1;

        /// <summary>Independent streams so decoration sampling can never shift rewards.</summary>
        public enum Channel
        {
            Route = 1,
            Terrain = 2,
            Decor = 3,
            Enemies = 4,
            Reward = 5,
            Weather = 6,
        }

        public static uint Hash(uint runSeed, int islandId, Channel channel, int version = GeneratorVersion)
        {
            // FNV-1a, 32-bit
            uint h = 2166136261u;
            h = Step(h, (byte)(runSeed & 0xFF));
            h = Step(h, (byte)((runSeed >> 8) & 0xFF));
            h = Step(h, (byte)((runSeed >> 16) & 0xFF));
            h = Step(h, (byte)((runSeed >> 24) & 0xFF));
            h = Step(h, (byte)(islandId & 0xFF));
            h = Step(h, (byte)((islandId >> 8) & 0xFF));
            h = Step(h, (byte)channel);
            h = Step(h, (byte)(version & 0xFF));
            h = Step(h, (byte)((version >> 8) & 0xFF));
            return h;
        }

        static uint Step(uint h, byte b)
        {
            h ^= b;
            h *= 16777619u;
            return h;
        }

        /// <summary>
        /// A cheap deterministic RNG seeded from the derived hash.
        /// NOTE: this is a CLASS, not a struct. As a struct it was passed around by value and
        /// every call site got an identical copy — the state never advanced, so every island
        /// rolled the same biome/element/risk. A class keeps one shared, advancing state.
        /// </summary>
        public sealed class Rng
        {
            uint state;
            public Rng(uint seed) { state = seed == 0u ? 1u : seed; }
            public uint NextUInt()
            {
                // xorshift32
                uint x = state;
                x ^= x << 13; x ^= x >> 17; x ^= x << 5;
                state = x;
                return x;
            }
            public float Next01() => (NextUInt() & 0xFFFFFF) / 16777216f;
            public int Range(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive) return minInclusive;
                return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
            }
            public float Range(float min, float max) => min + Next01() * (max - min);
            public T Pick<T>(IList<T> list) => list == null || list.Count == 0 ? default : list[Range(0, list.Count)];
        }
    }
}
