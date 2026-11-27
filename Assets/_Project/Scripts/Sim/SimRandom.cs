using System;

namespace RajaSawit.Sim
{
    /// <summary>
    /// Seeded xorshift32 random. Its state lives inside GameState, so a saved game
    /// replays exactly the same pests, wind and fire spread.
    /// </summary>
    [Serializable]
    public class SimRandom
    {
        // Stored as int so Unity's JsonUtility serializes it without trouble.
        public int state = 1;

        public SimRandom() { }

        public SimRandom(int seed) { Seed(seed); }

        public void Seed(int seed)
        {
            state = seed == 0 ? 0x6D2B79F5 : seed;
        }

        public uint NextUInt()
        {
            unchecked
            {
                uint x = (uint)state;
                if (x == 0) x = 0x6D2B79F5;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                state = (int)x;
                return x;
            }
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Uniform int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }
    }
}
