#if MILEASE_HAS_NEWTONSOFT
using Newtonsoft.Json;
#endif

namespace Milestro.Skia
{
    public class FontFaceInfo
    {
#if MILEASE_HAS_NEWTONSOFT
        [JsonProperty("sourcePath")]
#endif
        public string SourcePath { get; set; }

#if MILEASE_HAS_NEWTONSOFT
        [JsonProperty("familyName")]
#endif
        public string FamilyName { get; set; }

#if MILEASE_HAS_NEWTONSOFT
        [JsonProperty("faceIndex")]
#endif
        public int FaceIndex { get; set; }

#if MILEASE_HAS_NEWTONSOFT
        [JsonProperty("instanceIndex")]
#endif
        public int InstanceIndex { get; set; }

#if MILEASE_HAS_NEWTONSOFT
        [JsonProperty("packedIndex")]
#endif
        public int PackedIndex { get; set; }

#if MILEASE_HAS_NEWTONSOFT
        [JsonProperty("weight")]
#endif
        public int Weight { get; set; }

#if MILEASE_HAS_NEWTONSOFT
        [JsonProperty("width")]
#endif
        public int Width { get; set; }

#if MILEASE_HAS_NEWTONSOFT
        [JsonProperty("slant")]
#endif
        public int Slant { get; set; }

#if MILEASE_HAS_NEWTONSOFT
        [JsonProperty("fixedPitch")]
#endif
        public bool FixedPitch { get; set; }
    }
}
