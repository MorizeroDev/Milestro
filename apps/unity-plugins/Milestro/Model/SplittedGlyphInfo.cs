using System.Collections.Generic;
#if MILEASE_HAS_NEWTONSOFT_JSON
using Newtonsoft.Json;
#endif

namespace Milestro.Model
{
    public class SplittedGlyphInfo
    {
#if MILEASE_HAS_NEWTONSOFT_JSON
        [JsonProperty("bounds")]
#endif
        public List<Bound> Bounds { get; set; }
    }
}
