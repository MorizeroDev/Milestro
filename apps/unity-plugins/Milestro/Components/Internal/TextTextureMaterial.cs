using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Milestro.Components.Internal
{
    internal static class TextTextureMaterial
    {
        private static Material? material;

        internal static Material? Material
        {
            get
            {
                if (material != null)
                {
                    return material;
                }

                var shader = Resources.Load<Shader>("Milestro/TextPremultiplied");
                if (shader == null || !shader.isSupported)
                {
                    return null;
                }

                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                Application.quitting += Reset;
#if UNITY_EDITOR
                AssemblyReloadEvents.beforeAssemblyReload += Reset;
#endif
                return material;
            }
        }

        internal static bool IsOwned(Material candidate) => material != null && candidate == material;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Application.quitting -= Reset;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= Reset;
#endif
            if (material != null)
            {
                if (Application.isPlaying) Object.Destroy(material);
                else Object.DestroyImmediate(material);
            }
            material = null;
        }
    }
}
