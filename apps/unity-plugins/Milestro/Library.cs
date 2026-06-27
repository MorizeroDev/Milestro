using System;
using UnityEngine;

namespace Milestro
{
    public static class Library
    {
        public struct Version
        {
            public int Major;
            public int Minor;
            public int Patch;

            public override string ToString() => $"{Major}.{Minor}.{Patch}";
        }

        public static bool GetVersion(out Version version)
        {
            version = new Version();
            long result = Binding.BindingC.GetVersion(out var major, out var minor, out var patch);

            if (result == major)
            {
                version.Major = major;
                version.Minor = minor;
                version.Patch = patch;
                return true;
            }
            else
            {
                Debug.LogError($"Failed to get Milestro version. Error code: {result}");
                return false;
            }
        }

    }
}