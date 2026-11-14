using System;
using System.Collections.Generic;
using Milestro.Skia;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components.Internal
{
    internal interface ITextPreparationParticipant
    {
        bool IsPreparationActive { get; }
        void PrepareText();
    }

    internal sealed class TextPreparationQueue
    {
        private readonly HashSet<ITextPreparationParticipant> pending =
            new HashSet<ITextPreparationParticipant>();
        private readonly List<ITextPreparationParticipant> batch =
            new List<ITextPreparationParticipant>();
        private readonly HashSet<ITextPreparationParticipant> cancelled =
            new HashSet<ITextPreparationParticipant>();
        private bool processing;

        internal int Count => pending.Count;

        internal void Clear()
        {
            pending.Clear();
            batch.Clear();
            cancelled.Clear();
            processing = false;
        }

        internal void Enqueue(ITextPreparationParticipant participant)
        {
            pending.Add(participant);
        }

        internal void Remove(ITextPreparationParticipant participant)
        {
            pending.Remove(participant);
            if (processing)
            {
                cancelled.Add(participant);
            }
        }

        internal void Process()
        {
            if (processing || pending.Count == 0)
            {
                return;
            }

            processing = true;
            batch.AddRange(pending);
            pending.Clear();
            try
            {
                foreach (var participant in batch)
                {
                    if (cancelled.Contains(participant) || !participant.IsPreparationActive)
                    {
                        continue;
                    }

                    try
                    {
                        participant.PrepareText();
                    }
                    catch (Exception error)
                    {
                        Debug.LogException(error);
                    }
                }
            }
            finally
            {
                batch.Clear();
                cancelled.Clear();
                processing = false;
            }
        }
    }

    internal sealed class TextPreparationScheduler : ICanvasElement
    {
        private static readonly TextPreparationScheduler Instance = new TextPreparationScheduler();
        private static readonly TextPreparationQueue Queue = new TextPreparationQueue();
        private static bool registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            UnityEngine.Canvas.preWillRenderCanvases -= RegisterLayoutPass;
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(Instance);
            Queue.Clear();
            registered = false;
        }

        public Transform transform => null!;

        internal static void Request(ITextPreparationParticipant participant)
        {
            if (!participant.IsPreparationActive)
            {
                return;
            }

            Queue.Enqueue(participant);
            if (!registered)
            {
                registered = true;
                UnityEngine.Canvas.preWillRenderCanvases += RegisterLayoutPass;
            }

            RegisterLayoutPass();
        }

        internal static void Cancel(ITextPreparationParticipant participant)
        {
            Queue.Remove(participant);
        }

        private static void RegisterLayoutPass()
        {
            if (Queue.Count != 0 && !CanvasUpdateRegistry.IsRebuildingLayout())
            {
                CanvasUpdateRegistry.TryRegisterCanvasElementForLayoutRebuild(Instance);
            }
        }

        public void Rebuild(CanvasUpdate executing)
        {
            if (executing != CanvasUpdate.PostLayout)
            {
                return;
            }

            Queue.Process();
            TextGlyphAtlas.Flush();
            RenderTextureAtlas.Flush();
        }

        public void LayoutComplete() { }
        public void GraphicUpdateComplete() { }
        public bool IsDestroyed() => false;
    }
}
