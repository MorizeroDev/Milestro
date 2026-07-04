#if MILEASE_HAS_NEWTONSOFT_JSON
using Newtonsoft.Json;
#endif

namespace Milestro.Model
{
    public class Bound
    {
#if MILEASE_HAS_NEWTONSOFT_JSON
        [JsonProperty("left")]
#endif
        public float Left { get; set; }

#if MILEASE_HAS_NEWTONSOFT_JSON
        [JsonProperty("top")]
#endif
        public float Top { get; set; }

#if MILEASE_HAS_NEWTONSOFT_JSON
        [JsonProperty("right")]
#endif
        public float Right { get; set; }

#if MILEASE_HAS_NEWTONSOFT_JSON
        [JsonProperty("bottom")]
#endif
        public float Bottom { get; set; }
    }
}
