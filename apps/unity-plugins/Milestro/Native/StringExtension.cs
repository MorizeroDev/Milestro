using System.Linq;
using System.Text;

namespace Milestro.Native
{
    public static class StringExtension
    {
        public static byte[] CStr(this string self)
        {
            return Encoding.UTF8.GetBytes(self).Append<byte>(0).ToArray();
        }
    }
}
