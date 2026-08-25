using System.Collections.Generic;
using Milestro.RichTextParser;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal readonly struct TextBoxLinkGeometrySnapshot
    {
        internal TextBoxLinkGeometrySnapshot(Paragraph paragraph,
            IReadOnlyList<LinkAnnotation> links,
            Vector2 paintPosition,
            Rect clipRect,
            Vector2Int visibleOutputSize)
        {
            Paragraph = paragraph;
            Links = links;
            PaintPosition = paintPosition;
            ClipRect = clipRect;
            VisibleOutputSize = visibleOutputSize;
        }

        internal Paragraph Paragraph { get; }
        internal IReadOnlyList<LinkAnnotation> Links { get; }
        internal Vector2 PaintPosition { get; }
        internal Rect ClipRect { get; }
        internal Vector2Int VisibleOutputSize { get; }
    }

    internal struct TextBoxLinkGeometryPublication
    {
        private long nextToken;
        private long generation;
        private long pendingToken;
        private bool pending;
        private bool pendingHasGeometry;
        private bool rebuildRequested;
        private TextBoxLinkGeometrySnapshot pendingSnapshot;
        private int ordinarySubmissionsPending;
        private bool controlledSubmissionRequired;
        private bool published;
        private TextBoxLinkGeometrySnapshot publishedSnapshot;

        internal bool HasPending => pending;
        internal bool RequiresControlledSubmission => controlledSubmissionRequired;
        internal bool CanBeginControlledSubmission => !pending && ordinarySubmissionsPending == 0;

        internal void RequireControlledSubmission()
        {
            controlledSubmissionRequired = true;
        }

        internal void TrackOrdinarySubmission()
        {
            if (controlledSubmissionRequired || pending)
            {
                throw new System.InvalidOperationException(
                    "Ordinary TextBox submissions cannot overlap controlled link publication.");
            }

            ++ordinarySubmissionsPending;
        }

        internal bool CompleteOrdinarySubmission()
        {
            if (ordinarySubmissionsPending <= 0)
            {
                return false;
            }

            --ordinarySubmissionsPending;
            return true;
        }

        internal bool TryBegin(bool hasGeometry,
            TextBoxLinkGeometrySnapshot snapshot,
            out long token)
        {
            token = 0;
            if (hasGeometry)
            {
                controlledSubmissionRequired = true;
            }
            if (!CanBeginControlledSubmission)
            {
                return false;
            }

            InvalidatePublished();
            token = NextToken();
            pending = true;
            pendingToken = token;
            pendingHasGeometry = hasGeometry;
            pendingSnapshot = hasGeometry ? snapshot : default;
            rebuildRequested = false;
            return true;
        }

        internal void RequestRebuild()
        {
            if (pending)
            {
                rebuildRequested = true;
            }
        }

        internal bool Complete(long token,
            UnitySkiaRenderTextureSurface.RenderSubmissionStatus status,
            out bool needsRebuild)
        {
            needsRebuild = false;
            if (!pending || token == 0 || token != pendingToken)
            {
                return false;
            }

            needsRebuild = rebuildRequested;
            var completedWithGeometry = pendingHasGeometry;
            var publish = status == UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn &&
                          completedWithGeometry;
            var snapshot = pendingSnapshot;
            ClearPending();
            if (publish)
            {
                AdvanceGeneration();
                publishedSnapshot = snapshot;
                published = true;
            }
            else if (status == UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn &&
                     !completedWithGeometry &&
                     !needsRebuild)
            {
                controlledSubmissionRequired = false;
            }

            return true;
        }

        internal bool Cancel(long token, out bool needsRebuild)
        {
            needsRebuild = false;
            if (!pending || token == 0 || token != pendingToken)
            {
                return false;
            }

            needsRebuild = rebuildRequested;
            ClearPending();
            return true;
        }

        internal void InvalidatePublished()
        {
            if (!published)
            {
                return;
            }

            AdvanceGeneration();
            published = false;
            publishedSnapshot = default;
        }

        internal bool TryGetPublished(out TextBoxLinkGeometrySnapshot snapshot, out long publishedGeneration)
        {
            snapshot = publishedSnapshot;
            publishedGeneration = generation;
            return published;
        }

        internal void Reset()
        {
            InvalidatePublished();
            ClearPending();
            ordinarySubmissionsPending = 0;
            controlledSubmissionRequired = false;
        }

        private long NextToken()
        {
            unchecked
            {
                ++nextToken;
                if (nextToken == 0)
                {
                    ++nextToken;
                }

                return nextToken;
            }
        }

        private void AdvanceGeneration()
        {
            unchecked
            {
                ++generation;
                if (generation == 0)
                {
                    ++generation;
                }
            }
        }

        private void ClearPending()
        {
            pending = false;
            pendingToken = 0;
            pendingHasGeometry = false;
            pendingSnapshot = default;
            rebuildRequested = false;
        }
    }
}
