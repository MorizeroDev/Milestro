using System;
using Milestro.Configuration;
using Milestro.Skia;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milestro.AndroidValidation
{
    // Test-only bootstrap. Configure before the Demo's AfterSceneLoad runner creates surfaces.
    public static class AndroidValidationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            TextAsset asset = Resources.Load<TextAsset>("MilestroAndroidValidationMode");
            if (asset == null)
                return; // Editor preview, outside a validation player build.
            string mode = asset.text.Trim();
            if (mode != "VulkanDirect" && mode != "VulkanStagingCopy" && mode != "GLES3")
                throw new InvalidOperationException($"Unknown Android validation mode: {mode}");
            MilestroConfiguration.Configuration.RenderSurface.VulkanBackend =
                mode == "VulkanStagingCopy" ? UnitySkiaVulkanBackend.StagingCopy : UnitySkiaVulkanBackend.Direct;
            GraphicsDeviceType expected = mode == "GLES3" ? GraphicsDeviceType.OpenGLES3 : GraphicsDeviceType.Vulkan;
            Debug.Log($"[AndroidValidation] mode={mode} api={SystemInfo.graphicsDeviceType} " +
                      $"device={SystemInfo.graphicsDeviceName} version={SystemInfo.graphicsDeviceVersion}");
            if (SystemInfo.graphicsDeviceType != expected)
                throw new InvalidOperationException($"Expected {expected}, got {SystemInfo.graphicsDeviceType}");
        }
    }
}
