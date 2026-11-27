using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Milestro.ResourceManagement
{
    public sealed class SpriteRequest
    {
        public SpriteRequest(IDictionary<string, string> attributes, string content)
        {
            if (attributes == null) throw new ArgumentNullException(nameof(attributes));
            Attributes = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(attributes));
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public IReadOnlyDictionary<string, string> Attributes { get; }
        public string Content { get; }
    }
}
