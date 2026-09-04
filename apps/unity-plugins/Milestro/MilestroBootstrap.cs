#if UNITY_EDITOR
using UnityEditor;
#endif
using Milestro.Input;
using Milestro.Unicode;
using UnityEngine;

namespace Milestro
{
    internal static class MilestroBootstrap
    {
        private static int _initialized;

        private static void InitIcu()
        {
#if !MILESTRO_NO_ICU_INIT
            if (System.Threading.Interlocked.Exchange(ref _initialized, 1) == 1)
                return;

            try
            {
                IcuInitializer.Init();
            }
            catch
            {
                // A failed editor load must not suppress a later runtime attempt.
                System.Threading.Interlocked.Exchange(ref _initialized, 0);
                throw;
            }
#endif
        }

        private static void Boot()
        {
            InitIcu();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeBoot()
        {
            Boot();
            HybridInputRuntime.ResetAndInitialize();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void EditorBoot()
        {
            try
            {
                Boot();
            }
            catch (System.DllNotFoundException) when (Application.isBatchMode)
            {
                // Cross-compiling a player does not require a host-editor native
                // binary. RuntimeBoot still fails normally if that player cannot
                // load its own library; do not silently skip player initialization.
                Debug.LogWarning("Milestro: host native library unavailable during batch import. " +
                                 "ICU initialization is deferred until runtime; editor previews require a host plugin.");
            }
        }
#endif
    }
}
