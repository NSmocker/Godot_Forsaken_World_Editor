using System;
using System.Linq;

namespace SilverEcho.ForsakenWorld
{
    [Serializable]
    public sealed class AngelicaMatrix
    {
        private readonly float[] values;

        public AngelicaMatrix(float[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Length != 16)
                throw new ArgumentException("An Angelica matrix must contain 16 values.", nameof(values));

            this.values = values.ToArray();
        }

        public float[] Values => values.ToArray();
    }
}
