#if MILEASE_HAS_NEWTONSOFT
using Newtonsoft.Json;
#endif

namespace Milestro.Skia
{
    public class FontFamilyName
    {
#if MILEASE_HAS_NEWTONSOFT
        [JsonProperty("name")]
#endif
        public string Name { get; set; }

#if MILEASE_HAS_NEWTONSOFT
        [JsonProperty("language")]
#endif
        public string Language { get; set; }
    }
}
