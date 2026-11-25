using System;
using System.Collections.Generic;

namespace Milestro.ResourceManagement
{
    public sealed class ResourceProviderRouter<TProvider> where TProvider : class
    {
        private readonly Dictionary<string, TProvider> providers =
            new Dictionary<string, TProvider>(StringComparer.OrdinalIgnoreCase);
        private TProvider defaultProvider;

        public ResourceProviderRouter(TProvider defaultProvider)
        {
            this.defaultProvider = defaultProvider ?? throw new ArgumentNullException(nameof(defaultProvider));
        }

        public TProvider DefaultProvider
        {
            get => defaultProvider;
            set => defaultProvider = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void Register(string scheme, TProvider provider)
        {
            ValidateScheme(scheme);
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (providers.ContainsKey(scheme))
                throw new InvalidOperationException($"A resource provider is already registered for '{scheme}'.");
            providers.Add(scheme, provider);
        }

        public void Replace(string scheme, TProvider provider)
        {
            ValidateScheme(scheme);
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (!providers.ContainsKey(scheme))
                throw new InvalidOperationException($"No resource provider is registered for '{scheme}'.");
            providers[scheme] = provider;
        }

        public bool Unregister(string scheme)
        {
            ValidateScheme(scheme);
            return providers.Remove(scheme);
        }

        public TProvider Resolve(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var separator = source.IndexOf("://", StringComparison.Ordinal);
            if (separator <= 0 || !IsScheme(source, separator)) return defaultProvider;

            var scheme = source.Substring(0, separator);
            if (providers.TryGetValue(scheme, out var provider)) return provider;
            throw new InvalidOperationException($"No resource provider is registered for '{scheme}'.");
        }

        private static void ValidateScheme(string scheme)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));
            if (scheme.Length == 0 || !IsScheme(scheme, scheme.Length))
                throw new ArgumentException("Scheme must match [A-Za-z][A-Za-z0-9+.-]*.", nameof(scheme));
        }

        private static bool IsScheme(string value, int length)
        {
            if (!IsAsciiLetter(value[0])) return false;
            for (var index = 1; index < length; ++index)
            {
                var character = value[index];
                if (!IsAsciiLetter(character) && !(character >= '0' && character <= '9') &&
                    character != '+' && character != '.' && character != '-') return false;
            }
            return true;
        }

        private static bool IsAsciiLetter(char character)
        {
            return character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z';
        }
    }
}
