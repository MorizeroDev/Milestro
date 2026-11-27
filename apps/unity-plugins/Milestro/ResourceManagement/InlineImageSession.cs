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
        internal InlineResourceBorrow? Borrow { get; private set; }
        internal ImageResource? Resource => Borrow?.Resource;
        internal Vector2 IntrinsicSize => Resource?.IntrinsicSize ?? Vector2.zero;
        internal Sprite? Sprite => Resource?.Sprite;

        internal async void Load(string? source, SpriteRequest? request, Action changed)
        {
            try
            {
                var acquired = request != null
                    ? await SpriteResources.BorrowAsync(request, cancellation.Token)
                    : await ImageResources.BorrowAsync(source!, cancellation.Token);
                if (disposed) acquired?.Dispose();
                else Borrow = acquired;
                if (!disposed) changed();
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
            Borrow?.Dispose();
            Borrow = null;
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
                if (annotation.Source == null && annotation.SpriteRequest == null) continue;
                var key = annotation.StartUtf16.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    (annotation.SpriteRequest != null ? ":sprite" : ":img:" + annotation.Source);
                if (!bindings.TryGetValue(key, out var binding))
                {
                    binding = new InlineImageBinding();
                    bindings.Add(key, binding);
                    binding.Load(annotation.Source, annotation.SpriteRequest, () => Changed = true);
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
