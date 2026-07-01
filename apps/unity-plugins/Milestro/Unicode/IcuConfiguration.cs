using System;
using System.IO;
using UnityEngine;

namespace Milestro.Unicode
{
    public static class IcuConfiguration
    {
        public static string IcudtlPath => Path.Combine(Application.persistentDataPath, "icudtl.dat");

        private static bool initialized;

        private static readonly Lazy<bool> InitDelegate = new Lazy<bool>(() =>
        {
            var db = Resources.Load<TextAsset>("Milestro/icudtl.dat");
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
            File.WriteAllBytes(IcudtlPath, db.bytes);

            Debug.Log($"loading ICU from {IcudtlPath}");
            try
            {
                Icu.LoadIcuFronPath(IcudtlPath);
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
