using System.Collections.Generic;
using System.Linq;

namespace Casocket
{
    internal static class Extensions
    {
        public static byte[] TrimEnd(this IEnumerable<byte> byteArray, int index)
        {
            var trimmed = byteArray.Take(index);

            return trimmed.ToArray();
        }
    }
}
