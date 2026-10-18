using System;
using Milestro.Model;
using Milestro.Skia;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal interface ITextBoxRenderTarget : IDisposable
    {
        Texture? OutputTexture { get; }
        Rect OutputUvRect { get; }
        int OutputWidth { get; }
        int OutputHeight { get; }
        bool HasOutput { get; }
        long OutputVersion { get; }
        Vector2 ScrollOffset { get; }
        Vector2 ContentSize { get; }
        Vector2 ViewportSize { get; }
        Vector2 MaxScrollOffset { get; }
        TextBoxHorizontalScrollState HorizontalScrollState { get; }

        event Action<UnitySkiaRenderTextureSurface.RenderSubmissionStatus>? RenderEventCompleted;

        void MarkPropertiesChanged();
        void MarkPaintChanged();
        bool TryHitLink(Vector2 normalizedViewportPoint, out TextBoxLinkHit hit);
        bool TryGetContentSnapshot(out TextBoxContentSnapshot snapshot);
        bool TryGetLinkGeometrySnapshot(out TextBoxLinkGeometrySnapshot snapshot, out long generation);
        bool Rebuild(TextBoxRenderViewport viewport,
            ColorSpace colorSpace,
            TextBoxRenderTargetSettings settings,
            bool forceText,
            UnityEngine.Object? logContext);
    }
}
