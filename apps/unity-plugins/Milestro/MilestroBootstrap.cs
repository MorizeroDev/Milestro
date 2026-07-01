#if UNITY_EDITOR
using UnityEditor;
#endif
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

            IcuConfiguration.Init();
#endif
        }

        private static void Boot()
        {
            InitIcu();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RuntimeBoot()
        {
            Boot();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void EditorBoot()
        {
            Boot();
        }
#endif
    }
}
