using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Milestro.TextInputLifecycleQA
{
    public sealed class TextInputLifecycleQaBootstrap : MonoBehaviour
    {
        [SerializeField] private string targetScenePath = string.Empty;
        [SerializeField] private string sourceHead = string.Empty;
        [SerializeField] private string sourceTree = string.Empty;

        public string TargetScenePath => targetScenePath;
        public string SourceHead => sourceHead;
        public string SourceTree => sourceTree;

        public void Configure(string scenePath, string exactSourceHead, string exactSourceTree)
        {
            targetScenePath = scenePath;
            sourceHead = exactSourceHead;
            sourceTree = exactSourceTree;
        }

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(targetScenePath))
            {
                CompleteFailure("Bootstrap target scene path is missing.");
                yield break;
            }

            TextInputLifecycleQaStableRecorder.ArmForTargetScene(sourceHead, sourceTree);
            AsyncOperation? operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(targetScenePath, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                CompleteFailure(exception.GetType().Name + ": " + exception.Message);
                yield break;
            }
            if (operation == null)
            {
                CompleteFailure("Bootstrap could not start the target scene load.");
                yield break;
            }
            yield return operation;
        }

        private void CompleteFailure(string message)
        {
            var result = new TextInputLifecycleQaResult
            {
                status = "FAIL",
                sourceHead = sourceHead,
                sourceTree = sourceTree,
                message = message
            };
            Debug.Log("TASK159_QA_RESULT " + JsonUtility.ToJson(result));
#if !UNITY_EDITOR
            Application.Quit(1);
#endif
        }
    }
}
