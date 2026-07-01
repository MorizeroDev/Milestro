using Milestro.Skia;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Milestro.Editor
{
    public static class MilestroFontRegistryEditor
    {
        [MenuItem("Milestro/Font Registry/List Registered Font Families")]
        public static void ListFonts()
        {
            var statistic = FontRegistry.GetRegisteredFontFamilyNames();
            Debug.Log(JsonConvert.SerializeObject(statistic, Formatting.Indented));
        }

        [MenuItem("Milestro/Font Registry/List Registered Font Faces")]
        public static void ListFontFaces()
        {
            var statistic = FontRegistry.GetRegisteredFontFaces();
            Debug.Log(JsonConvert.SerializeObject(statistic, Formatting.Indented));
        }
    }
}
