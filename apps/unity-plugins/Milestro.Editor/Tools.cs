using System.Text;
using Milestro.Skia;
#if MILEASE_HAS_NEWTONSOFT_JSON
using Newtonsoft.Json;
#endif
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
#if MILEASE_HAS_NEWTONSOFT_JSON
            Debug.Log(JsonConvert.SerializeObject(statistic, Formatting.Indented));
#else
            Debug.Log(string.Join("\n", statistic));
#endif
        }


        [MenuItem("Milestro/Font Registry/List Registered Font Faces")]
        public static void ListFontFaces()
        {
            var statistic = FontRegistry.GetRegisteredFontFaces();
#if MILEASE_HAS_NEWTONSOFT_JSON
           Debug.Log(JsonConvert.SerializeObject(statistic, Formatting.Indented));
#else
            var builder = new StringBuilder();
            foreach (var face in statistic)
            {
                builder.AppendLine(
                    $"{face.FamilyName} | source={face.SourcePath} | face={face.FaceIndex} | instance={face.InstanceIndex} | packed={face.PackedIndex} | weight={face.Weight} | width={face.Width} | slant={face.Slant} | fixedPitch={face.FixedPitch}");
            }
            Debug.Log(builder.ToString());
#endif
        }
    }
}
