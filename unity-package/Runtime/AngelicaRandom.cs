using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>ARandomGen used by CELGrassBlock::BuildGrasses (Marsaglia uniform).</summary>
    public sealed class AngelicaRandom
    {
        private readonly double[] u = new double[97];
        private double c;
        private double cd;
        private double cm;
        private int i97;
        private int j97;
        private bool ready;

        public void Init(int seed)
        {
            var dw = seed < 0 ? (uint)seed : (uint)seed;
            RandomInitialize((int)(dw % 31328u), (int)(dw % 30081u));
        }

        public float RandomFloat(float lower, float upper)
        {
            return (float)((upper - lower) * RandomUniform() + lower);
        }

        private double RandomUniform()
        {
            if (!ready)
                RandomInitialize(1802, 9373);
            var uni = u[i97 - 1] - u[j97 - 1];
            if (uni <= 0.0)
                uni += 1.0;
            u[i97 - 1] = uni;
            if (--i97 == 0)
                i97 = 97;
            if (--j97 == 0)
                j97 = 97;
            if ((c -= cd) < 0.0)
                c += cm;
            if ((uni -= c) < 0.0)
                uni += 1.0;
            return uni;
        }

        private void RandomInitialize(int ij, int kl)
        {
            if (ij < 0 || ij > 31328 || kl < 0 || kl > 30081)
            {
                ij = 1802;
                kl = 9373;
            }

            var i = (ij / 177) % 177 + 2;
            var j = (ij % 177) + 2;
            var k = (kl / 169) % 178 + 1;
            var l = kl % 169;
            for (var ii = 0; ii < 97; ii++)
            {
                double s = 0.0;
                var t = 0.5;
                for (var jj = 0; jj < 24; jj++)
                {
                    var m = (((i * j) % 179) * k) % 179;
                    i = j;
                    j = k;
                    k = m;
                    l = (53 * l + 1) % 169;
                    if (((l * m) % 64) >= 32)
                        s += t;
                    t *= 0.5;
                }

                u[ii] = s;
            }

            c = 362436.0 / 16777216.0;
            cd = 7654321.0 / 16777216.0;
            cm = 16777213.0 / 16777216.0;
            i97 = 97;
            j97 = 33;
            ready = true;
        }
    }
}
