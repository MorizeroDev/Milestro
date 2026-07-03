using System.IO;
using UnityEngine;

namespace Milestro.Configuration
{
    public class IcuConfiguration
    {
        public string IcudtlResourcePath { get; set; } = "Milestro/icudtl.dat";
        public string IcudtlPersistentFileName { get; set; } = "icudtl.dat";
        public string IcudtlPathOverride { get; set; } = "";

        public string IcudtlPath => string.IsNullOrEmpty(IcudtlPathOverride)
            ? Path.Combine(Application.persistentDataPath, IcudtlPersistentFileName)
            : IcudtlPathOverride;
    }
}
