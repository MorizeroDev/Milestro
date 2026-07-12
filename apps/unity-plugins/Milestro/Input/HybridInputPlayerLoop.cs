using System;
using UnityEngine.LowLevel;

namespace Milestro.Input
{
    /// <summary>
    /// Installs the single input drain immediately after Unity's script Update stage.
    /// </summary>
    internal static class HybridInputPlayerLoop
    {
        private static PlayerLoopSystem.UpdateFunction? updateFunction;

        internal static bool Install(PlayerLoopSystem.UpdateFunction callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            if (!Configure(ref playerLoop, callback))
            {
                return false;
            }

            PlayerLoop.SetPlayerLoop(playerLoop);
            return true;
        }

        internal static void Uninstall()
        {
            updateFunction = null;
            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            if (RemoveDispatchStages(ref playerLoop) == 0)
            {
                return;
            }

            PlayerLoop.SetPlayerLoop(playerLoop);
        }

        internal static bool Configure(ref PlayerLoopSystem playerLoop,
            PlayerLoopSystem.UpdateFunction callback)
        {
            RemoveDispatchStages(ref playerLoop);
            if (!InsertAfterScriptUpdate(ref playerLoop))
            {
                updateFunction = null;
                return false;
            }

            updateFunction = callback;
            return true;
        }

        internal static int RemoveDispatchStages(ref PlayerLoopSystem playerLoop)
        {
            var removed = 0;
            var systems = playerLoop.subSystemList;
            if (systems == null || systems.Length == 0)
            {
                return removed;
            }

            for (var i = 0; i < systems.Length; ++i)
            {
                var child = systems[i];
                removed += RemoveDispatchStages(ref child);
                systems[i] = child;
            }

            var retainedCount = 0;
            for (var i = 0; i < systems.Length; ++i)
            {
                if (systems[i].type == typeof(PostUpdateInputDispatch))
                {
                    ++removed;
                }
                else
                {
                    ++retainedCount;
                }
            }

            if (retainedCount == systems.Length)
            {
                playerLoop.subSystemList = systems;
                return removed;
            }

            var retained = new PlayerLoopSystem[retainedCount];
            var targetIndex = 0;
            for (var i = 0; i < systems.Length; ++i)
            {
                if (systems[i].type != typeof(PostUpdateInputDispatch))
                {
                    retained[targetIndex++] = systems[i];
                }
            }

            playerLoop.subSystemList = retained;
            return removed;
        }

        private static bool InsertAfterScriptUpdate(ref PlayerLoopSystem playerLoop)
        {
            var systems = playerLoop.subSystemList;
            if (systems == null)
            {
                return false;
            }

            for (var i = 0; i < systems.Length; ++i)
            {
                if (systems[i].type != typeof(UnityEngine.PlayerLoop.Update))
                {
                    continue;
                }

                var updatePhase = systems[i];
                var updateSystems = updatePhase.subSystemList;
                if (updateSystems == null)
                {
                    return false;
                }

                for (var j = 0; j < updateSystems.Length; ++j)
                {
                    if (updateSystems[j].type !=
                        typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate))
                    {
                        continue;
                    }

                    var configured = new PlayerLoopSystem[updateSystems.Length + 1];
                    Array.Copy(updateSystems, 0, configured, 0, j + 1);
                    configured[j + 1] = new PlayerLoopSystem
                    {
                        type = typeof(PostUpdateInputDispatch),
                        updateDelegate = RunDispatch
                    };
                    Array.Copy(updateSystems,
                        j + 1,
                        configured,
                        j + 2,
                        updateSystems.Length - j - 1);
                    updatePhase.subSystemList = configured;
                    systems[i] = updatePhase;
                    playerLoop.subSystemList = systems;
                    return true;
                }

                return false;
            }

            return false;
        }

        private static void RunDispatch()
        {
            updateFunction?.Invoke();
        }

        private sealed class PostUpdateInputDispatch
        {
        }
    }
}
