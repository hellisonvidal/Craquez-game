namespace Craquez.Sim
{
    /// <summary>RNG determinístico (xorshift32). Toda aleatoriedade da simulação passa por aqui.</summary>
    public class SimRng
    {
        public uint State;

        public SimRng(uint seed = 12345)
        {
            State = seed == 0 ? 0x9E3779B9u : seed;
        }

        public uint NextU32()
        {
            uint x = State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return x;
        }

        public float Next01() => (float)(NextU32() / 4294967296.0);

        public float Range(float a, float b) => a + (b - a) * Next01();

        public bool Chance(float p) => Next01() < p;

        /// <summary>Aproximação de normal (média 0, desvio ~1).</summary>
        public float Gauss() => (Next01() + Next01() + Next01() + Next01() - 2f) * 1.73f;
    }
}
