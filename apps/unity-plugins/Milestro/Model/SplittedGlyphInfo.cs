using System.Collections.Generic;
using Newtonsoft.Json;

namespace Milestro.Model
{
    public class SplittedGlyphInfo
    {
        [JsonProperty("bounds")] public List<Bound> Bounds { get; set; }
    }
}
