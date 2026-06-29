#if UNITY_EDITOR
using DefaultNamespace;
using Milestro.Skia;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class MilestroFontmanagerEditor
{
    [MenuItem("Milestro/Font Manager/Load Fonts")]
    public static void RegisterFonts()
    {
        FontAssetsManager.LoadFonts();
        ListFonts();
    }

    [MenuItem("Milestro/Font Manager/List Loaded Fonts")]
    public static void ListFonts()
    {
        var statistic = FontManager.GetFontFamilyNames();
        Debug.Log(JsonConvert.SerializeObject(statistic, Formatting.Indented));
    }

    [MenuItem("Milestro/Font Manager/List Loaded Font Faces")]
    public static void ListFontFaces()
    {
        var statistic = FontManager.GetFontFaces();
        Debug.Log(JsonConvert.SerializeObject(statistic, Formatting.Indented));
    }
}
#endif
