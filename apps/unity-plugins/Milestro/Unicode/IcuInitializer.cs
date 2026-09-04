using System;
using Milestro.Configuration;
using UnityEngine;

namespace Milestro.Unicode
{
    internal static class IcuInitializer
    {
        private static bool initialized;

        private static readonly Lazy<bool> InitDelegate = new Lazy<bool>(() =>
        {
            var db = Resources.Load<TextAsset>(MilestroConfiguration.Configuration.Icu.IcudtlResourcePath);
            if (!db)
            {
                Debug.LogError("Failed to load icudtl.dat from Resources.");
                return false;
            }

            try
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                return PCLoad(db);
#else
                return MobileLoad(db);
#endif
            }
            finally
            {
                Resources.UnloadAsset(db);
            }
        });

        private static bool PCLoad(TextAsset db)
        {
            Debug.Log("loading ICU from memory");
            try
            {
                Icu.LoadIcuFronMemory(db.bytes);
            }
            catch (Exception e)
            {
                Debug.LogError("failed to load ICU");
                Debug.LogError(e);
                return false;
            }

            return true;
        }

        private static bool MobileLoad(TextAsset db)
        {
            // Avoid the early Android filesystem/mmap path. On some Unity IL2CPP
            // and vendor Android combinations it can crash during startup before
            // the native entrypoint is reached. The native implementation owns a
            // copy of the data, so this is safe after the TextAsset is released.
            Debug.Log("loading ICU from memory (Android)");
            try
            {
                Icu.LoadIcuFronMemory(db.bytes);
            }
            catch (Exception e)
            {
                Debug.LogError("failed to load ICU");
                Debug.LogError(e);
                return false;
            }

            return true;
        }

        public static void Init()
        {
            if (initialized)
            {
                return;
            }

            if (Icu.IsLoaded())
            {
                Debug.Log("ICU already loaded.");
                return;
            }

            var result = InitDelegate.Value;
            if (result)
            {
                initialized = true;
                Debug.Log("ICU configuration initialized successfully.");
            }
            else
            {
                Debug.LogError("Failed to initialize ICU configuration.");
                throw new Exception("ICU initialization failed. Please check the logs for more details.");
            }
        }
    }
}
