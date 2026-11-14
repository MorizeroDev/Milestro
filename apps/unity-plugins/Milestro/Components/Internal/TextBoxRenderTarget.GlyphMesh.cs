using System.Collections.Generic;
using Milestro.Configuration;
using Milestro.Model;
using Milestro.Skia;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal sealed partial class TextBoxRenderTarget
    {
        private readonly bool allowGlyphMesh;
        private readonly List<GlyphLayoutSnapshot> glyphLayouts = new List<GlyphLayoutSnapshot>();
        private readonly List<TextGlyphMeshRun> glyphMeshRuns = new List<TextGlyphMeshRun>();
        private bool glyphLayoutsPrepared;
        private bool glyphLayoutsSupported;
        private bool glyphMeshRequested;
        private bool glyphMeshPresentationAllowed = true;
        private int glyphMeshRequestedPageSize;
        private int glyphMeshRequestedPageCount;
        private int glyphMeshRequestedGlyphLimit;
        private bool glyphMeshBuildAttempted;
        private TextBoxRenderViewport glyphMeshViewport;
        private bool hasGlyphMeshViewport;
        private TextGlyphMeshOutput? glyphMeshOutput;

        internal TextBoxRenderTarget(bool allowGlyphMesh = true)
        {
            this.allowGlyphMesh = allowGlyphMesh;
        }

        internal TextGlyphMeshOutput? GlyphMeshOutput => glyphMeshOutput;

        private bool GlyphMeshConfigurationChanged
        {
            get
            {
                if (verticalLayoutActive)
                {
                    return false;
                }

                var configuration = MilestroConfiguration.Configuration.RenderSurface;
                var requested = allowGlyphMesh && glyphMeshPresentationAllowed && configuration.EnableTextGlyphMesh;
                return requested != glyphMeshRequested || requested &&
                    (configuration.GlyphAtlasPageSize != glyphMeshRequestedPageSize ||
                     configuration.MaxGlyphAtlasPages != glyphMeshRequestedPageCount ||
                     configuration.MaxTextGlyphMeshGlyphs != glyphMeshRequestedGlyphLimit);
            }
        }

        internal void SetGlyphMeshPresentationAllowed(bool allowed)
        {
            if (glyphMeshPresentationAllowed == allowed)
            {
                return;
            }

            glyphMeshPresentationAllowed = allowed;
            MarkPaintChanged();
        }

        private bool GlyphMeshViewportChanged(TextBoxRenderViewport viewport)
        {
            return glyphMeshRequested && (!hasGlyphMeshViewport || !glyphMeshViewport.Matches(viewport));
        }

        private bool TryRebuildGlyphMesh(TextBoxRenderTargetSettings settings,
            TextBoxRenderViewport viewport,
            Vector2Int layoutSizePixels,
            Vector2Int outputSizePixels,
            bool needsDraw)
        {
            var configuration = MilestroConfiguration.Configuration.RenderSurface;
            glyphMeshRequested = allowGlyphMesh && glyphMeshPresentationAllowed && configuration.EnableTextGlyphMesh;
            glyphMeshRequestedPageSize = configuration.GlyphAtlasPageSize;
            glyphMeshRequestedPageCount = configuration.MaxGlyphAtlasPages;
            glyphMeshRequestedGlyphLimit = configuration.MaxTextGlyphMeshGlyphs;
            glyphMeshViewport = viewport;
            hasGlyphMeshViewport = true;
            if (!glyphMeshRequested || placeholderAnnotations.Count > 0 || !EnsureGlyphLayouts())
            {
                ClearGlyphMeshOutput();
                return false;
            }

            if (!needsDraw && glyphMeshOutput != null && glyphMeshOutput.IsValid)
            {
                return true;
            }

            if (!needsDraw && glyphMeshOutput == null && glyphMeshBuildAttempted)
            {
                return false;
            }

            ResolveHorizontalPaintGeometry(settings, viewport, layoutSizePixels, outputSizePixels,
                out var paintPosition, out var clipRect);
            glyphMeshRuns.Clear();
            for (var index = 0; index < glyphLayouts.Count; ++index)
            {
                glyphMeshRuns.Add(new TextGlyphMeshRun(glyphLayouts[index],
                    ResolveHorizontalBlockPaintPosition(index, paintPosition, viewport.VisualScrollOffset.x),
                    fitScale));
            }

            var rasterScale = viewport.UseScreenSpaceRasterization ? viewport.DesiredRasterScale : 1f;
            glyphMeshBuildAttempted = true;
            if (!TextGlyphAtlas.TryBuild(glyphMeshRuns, rasterScale, outputSizePixels, clipRect,
                    out var nextOutput) || nextOutput == null)
            {
                ClearGlyphMeshOutput();
                return false;
            }

            try
            {
                DisposeSurface();
                linkGeometryPublication.Reset();
                var hasHitGeometry = paragraphs.Count > 0 &&
                    (linkAnnotations.Count > 0 || elementAnnotations.Count > 0) &&
                    clipRect.width > 0f && clipRect.height > 0f;
                if (hasHitGeometry)
                {
                    var hitGeometry = new TextBoxLinkGeometrySnapshot(contentVersion,
                        paragraphs.ToArray(), linkAnnotations,
                        BuildLinkGeometry(paragraphs, linkAnnotations, viewport.VisualScrollOffset.x),
                        paintPosition, clipRect, outputSizePixels, fitScale,
                        BuildPlaceholderGeometry(paintPosition, clipRect, viewport.VisualScrollOffset.x),
                        elementAnnotations,
                        BuildElementGeometry(paragraphs, elementAnnotations, viewport.VisualScrollOffset.x));
                    linkGeometryPublication.TryBegin(true, hitGeometry, out var token);
                    linkGeometryPublication.Complete(token,
                        UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn, out _);
                }
            }
            catch
            {
                nextOutput.Dispose();
                throw;
            }

            var previousOutput = glyphMeshOutput;
            glyphMeshOutput = nextOutput;
            previousOutput?.Dispose();
            MarkOutputChanged();
            return true;
        }

        private bool EnsureGlyphLayouts()
        {
            if (glyphLayoutsPrepared)
            {
                return glyphLayoutsSupported;
            }

            glyphLayoutsPrepared = true;
            glyphLayoutsSupported = false;
            foreach (var paragraph in paragraphs)
            {
                var layout = paragraph.CreateGlyphLayoutSnapshot();
                if (layout == null)
                {
                    DisposeGlyphLayouts();
                    return false;
                }

                glyphLayouts.Add(layout);
            }

            glyphLayoutsSupported = true;
            return true;
        }

        private void InvalidateGlyphLayouts()
        {
            DisposeGlyphLayouts();
            glyphLayoutsPrepared = false;
            glyphLayoutsSupported = false;
            glyphMeshBuildAttempted = false;
        }

        private void DisposeGlyphLayouts()
        {
            glyphMeshRuns.Clear();
            foreach (var layout in glyphLayouts)
            {
                layout.Dispose();
            }

            glyphLayouts.Clear();
        }

        private void ClearGlyphMeshOutput()
        {
            if (glyphMeshOutput == null)
            {
                return;
            }

            glyphMeshOutput.Dispose();
            glyphMeshOutput = null;
            MarkOutputChanged();
        }
    }
}
