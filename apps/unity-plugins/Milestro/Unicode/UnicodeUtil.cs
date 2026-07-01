using Milestro.Binding;
using Milestro.Model;
using Paraparty.UnityNative;

namespace Milestro.Unicode
{
    public static class UnicodeUtil
    {
        public static string IcuToLower(this string data, string locale = "en")
        {
            var localeCStr = locale.CStr();
            var dataCStr = data.CStr();
            unsafe
            {
                fixed (byte* localeC = localeCStr)
                {
                    fixed (byte* dataC = dataCStr)
                    {
                        ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeCaseMapToLower(out var value, localeC, dataC));
                        using var ret = new BytesWrapper(value);
                        return ret.GetString();
                    }
                }
            }
        }

        public static string IcuToUpper(this string data, string locale = "en")
        {
            var localeCStr = locale.CStr();
            var dataCStr = data.CStr();
            unsafe
            {
                fixed (byte* localeC = localeCStr)
                {
                    fixed (byte* dataC = dataCStr)
                    {
                        ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeCaseMapToUpper(out var value, localeC, dataC));
                        using var ret = new BytesWrapper(value);
                        return ret.GetString();
                    }
                }
            }
        }
    }
}
