using Newtonsoft.Json;

namespace Milestro.Skia
{
    public class FontFaceInfo
    {
        [JsonProperty("sourcePath")] public string SourcePath { get; set; }

        [JsonProperty("familyName")] public string FamilyName { get; set; }

        [JsonProperty("faceIndex")] public int FaceIndex { get; set; }

        [JsonProperty("instanceIndex")] public int InstanceIndex { get; set; }

        [JsonProperty("packedIndex")] public int PackedIndex { get; set; }

        [JsonProperty("weight")] public int Weight { get; set; }

        [JsonProperty("width")] public int Width { get; set; }

        [JsonProperty("slant")] public int Slant { get; set; }

        [JsonProperty("fixedPitch")] public bool FixedPitch { get; set; }
    }
}
