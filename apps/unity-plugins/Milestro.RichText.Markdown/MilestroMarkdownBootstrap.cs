#if UNITY_EDITOR
using UnityEditor;
#endif
using Milestro.RichTextParser;
using UnityEngine;

namespace Milestro.RichText.Markdown
{
    internal static class MilestroMarkdownBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistration()
        {
            RichTextParserService.Unregister(MarkdownRichTextParser.Id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRuntime()
        {
            Register();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditor()
        {
            Register();
        }
#endif

        private static void Register()
        {
            RichTextParserService.Register(new MarkdownRichTextParserProvider());
        }
    }
}
