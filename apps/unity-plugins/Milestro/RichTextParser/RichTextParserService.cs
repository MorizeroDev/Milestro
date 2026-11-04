using System;
using System.Collections.Generic;

namespace Milestro.RichTextParser
{
    /// <summary>
    /// Registry of rich text parser providers. Text boxes select a parser by id; modules
    /// register an <see cref="IRichTextParserProvider"/> per parser at startup. Registration
    /// replaces any previous provider with the same id and drops its shared instance.
    /// </summary>
    public static class RichTextParserService
    {
        private sealed class BuiltinXmlProvider : IRichTextParserProvider
        {
            public string Id => XmlRichTextParser.Id;
            public string DisplayName => XmlRichTextParser.DisplayName;
            public IRichTextParser CreateParser() => new XmlRichTextParser();
        }

        private static readonly object RegistryGate = new object();
        private static readonly Dictionary<string, IRichTextParserProvider> Providers =
            new Dictionary<string, IRichTextParserProvider>(StringComparer.Ordinal);
        private static readonly Dictionary<string, IRichTextParser> SharedInstances =
            new Dictionary<string, IRichTextParser>(StringComparer.Ordinal);
        private static bool builtinsRegistered;

        public static void Register(IRichTextParserProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            ValidateId(provider.Id);
            lock (RegistryGate)
            {
                EnsureBuiltins();
                Providers[provider.Id] = provider;
                SharedInstances.Remove(provider.Id);
            }
        }

        public static bool Unregister(string id)
        {
            lock (RegistryGate)
            {
                EnsureBuiltins();
                SharedInstances.Remove(id);
                return Providers.Remove(id);
            }
        }

        /// <summary>
        /// Creates the parser registered under <paramref name="id"/>. Parsers are stateless,
        /// so the created instance is shared with all later callers.
        /// </summary>
        public static IRichTextParser Create(string id)
        {
            if (TryCreate(id, out var parser) && parser != null)
            {
                return parser;
            }

            throw new ArgumentException(
                $"Unknown rich text parser id '{id}'. Registered ids: {string.Join(", ", Ids)}.",
                nameof(id));
        }

        public static bool TryCreate(string id, out IRichTextParser? parser)
        {
            parser = null;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            lock (RegistryGate)
            {
                EnsureBuiltins();
                if (SharedInstances.TryGetValue(id, out var shared))
                {
                    parser = shared;
                    return true;
                }

                if (!Providers.TryGetValue(id, out var provider))
                {
                    return false;
                }

                var created = provider.CreateParser();
                if (created == null)
                {
                    return false;
                }

                SharedInstances[id] = created;
                parser = created;
                return true;
            }
        }

        /// <summary>
        /// Snapshot of the registered providers, sorted by id. Used by the inspector
        /// dropdown; unknown ids typed by hand still work at runtime.
        /// </summary>
        public static IReadOnlyList<IRichTextParserProvider> Parsers
        {
            get
            {
                lock (RegistryGate)
                {
                    EnsureBuiltins();
                    var providers = new List<IRichTextParserProvider>(Providers.Values);
                    providers.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
                    return providers;
                }
            }
        }

        private static IReadOnlyList<string> Ids
        {
            get
            {
                var providers = Parsers;
                var ids = new List<string>(providers.Count);
                foreach (var provider in providers)
                {
                    ids.Add(provider.Id);
                }

                return ids;
            }
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Parser id must be a non-empty string.", nameof(id));
            }

            for (var i = 0; i < id.Length; i++)
            {
                var c = id[i];
                var isLetter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
                var isDigit = c >= '0' && c <= '9';
                var isValid = i == 0
                    ? isLetter || c == '_'
                    : isLetter || isDigit || c == '_' || c == '-';
                if (!isValid)
                {
                    throw new ArgumentException(
                        $"Parser id '{id}' must match [A-Za-z_][A-Za-z0-9_-]*: start with a " +
                        "letter or underscore and use only letters, digits, underscores, and hyphens.",
                        nameof(id));
                }
            }
        }

        private static void EnsureBuiltins()
        {
            if (builtinsRegistered)
            {
                return;
            }

            builtinsRegistered = true;
            Providers[XmlRichTextParser.Id] = new BuiltinXmlProvider();
        }
    }
}
