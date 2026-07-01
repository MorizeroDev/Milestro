using Newtonsoft.Json;

namespace Milestro.Model
{
    public class Bound
    {
        [JsonProperty("left")] public float Left { get; set; }

        [JsonProperty("top")] public float Top { get; set; }

        [JsonProperty("right")] public float Right { get; set; }

        [JsonProperty("bottom")] public float Bottom { get; set; }
    }
}
