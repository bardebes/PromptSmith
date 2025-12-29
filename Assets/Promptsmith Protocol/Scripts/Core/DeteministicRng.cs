using System.Collections.Generic;
namespace PromptsmithProtocol
{
    /// <summary>
    /// Small deterministic RNG (XorShift32). Stable across platforms/builds.
    /// </summary>
    public struct DeterministicRng
    {
        private uint _state;

        public DeterministicRng(int seed)
        {
            _state = (uint)seed;
            if (_state == 0) _state = 0x6D2B79F5u;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            var range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }

        public double NextDouble() => NextUInt() / (double)uint.MaxValue;

        public uint NextUInt()
        {
            // xorshift32
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = NextInt(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
