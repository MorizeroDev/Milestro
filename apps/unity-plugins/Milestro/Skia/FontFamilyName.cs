using Newtonsoft.Json;

namespace Milestro.Skia
{
    public class FontFamilyName
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("language")] public string Language { get; set; }
    }
}