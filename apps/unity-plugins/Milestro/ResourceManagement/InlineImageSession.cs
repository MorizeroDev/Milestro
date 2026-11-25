using System;
using System.Collections.Generic;
using System.Threading;
using Milestro.RichTextParser;
using UnityEngine;

namespace Milestro.ResourceManagement
{
    internal sealed class InlineImageBinding : IDisposable
    {
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool disposed;
        internal ImageResourceLease? Lease { get; private set; }
        internal Vector2 IntrinsicSize => Lease?.Resource.IntrinsicSize ?? Vector2.zero;
        internal Sprite? Sprite => Lease?.Resource.Sprite;

        internal async void Load(string source, Action changed)
        {
            try
            {
                var lease = await ImageResources.AcquireAsync(source, cancellation.Token);
                if (disposed) lease?.Dispose();
                else
                {
                    Lease = lease;
                    changed();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!disposed) Debug.LogException(error);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            cancellation.Cancel();
            Lease?.Dispose();
            Lease = null;
            cancellation.Dispose();
        }
    }

    internal sealed class InlineImageSession : IDisposable
    {
        private readonly Dictionary<string, InlineImageBinding> bindings = new Dictionary<string, InlineImageBinding>();
        private string? content;
        private string? parser;
        internal bool Changed { get; set; }

        internal void Bind(ParagraphPayload payload, string source, string? parserId)
        {
            if (source != content || parserId != parser)
            {
                Dispose();
                content = source;
                parser = parserId;
            }
            foreach (var annotation in payload.Placeholders)
            {
                if (annotation.Source == null) continue;
                var key = annotation.StartUtf16.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + annotation.Source;
                if (!bindings.TryGetValue(key, out var binding))
                {
                    binding = new InlineImageBinding();
                    bindings.Add(key, binding);
                    binding.Load(annotation.Source, () => Changed = true);
                }
                annotation.Presentation!.Binding = binding;
            }
        }

        public void Dispose()
        {
            foreach (var binding in bindings.Values) binding.Dispose();
            bindings.Clear();
            content = null;
            parser = null;
            Changed = false;
        }
    }
}
