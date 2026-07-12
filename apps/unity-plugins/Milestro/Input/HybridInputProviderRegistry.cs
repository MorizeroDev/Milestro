using System;
using System.Collections.Generic;

namespace Milestro.Input
{
    /// <summary>Stores registered candidates and selects at most one provider for an environment.</summary>
    internal sealed class HybridInputProviderRegistry
    {
        /// <summary>Registered candidates; at most one provider may be active.</summary>
        private readonly List<RegisteredProvider> providers = new List<RegisteredProvider>();
        private int epoch = 1;
        private long nextEntryId = 1L;

        internal HybridInputProviderHandle Register(IHybridInputProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }
            if (string.IsNullOrWhiteSpace(provider.Id))
            {
                throw new ArgumentException("Input provider ID must not be empty.", nameof(provider));
            }

            for (var i = 0; i < providers.Count; ++i)
            {
                if (ReferenceEquals(providers[i].Provider, provider))
                {
                    throw new InvalidOperationException($"Input provider '{provider.Id}' is already registered.");
                }
                if (string.Equals(providers[i].Provider.Id, provider.Id, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Input provider ID '{provider.Id}' is already registered.");
                }
            }

            var handle = new HybridInputProviderHandle(epoch, nextEntryId++);
            providers.Add(new RegisteredProvider(handle.EntryId, provider));
            return handle;
        }

        internal bool Unregister(HybridInputProviderHandle handle)
        {
            if (handle.Epoch != epoch)
            {
                return false;
            }

            for (var i = 0; i < providers.Count; ++i)
            {
                if (providers[i].EntryId != handle.EntryId)
                {
                    continue;
                }

                providers.RemoveAt(i);
                return true;
            }

            return false;
        }

        internal void Clear()
        {
            providers.Clear();
            epoch = unchecked(epoch + 1);
        }

        internal HybridInputProviderSelection Select(HybridInputEnvironment environment, string? overrideProviderId)
        {
            if (environment.EventSystemCount > 1)
            {
                return new HybridInputProviderSelection(HybridInputSelectionStatus.Conflict, null);
            }

            if (!string.IsNullOrEmpty(overrideProviderId))
            {
                return SelectOverride(environment, overrideProviderId);
            }

            IHybridInputProvider? selected = null;
            var selectedMatch = HybridInputProviderMatch.None;
            var selectedPriority = int.MinValue;
            var conflict = false;

            for (var i = 0; i < providers.Count; ++i)
            {
                var provider = providers[i].Provider;
                var match = provider.Match(environment);
                if (match == HybridInputProviderMatch.None)
                {
                    continue;
                }

                if (selected == null || match > selectedMatch ||
                    match == selectedMatch && provider.Priority > selectedPriority)
                {
                    selected = provider;
                    selectedMatch = match;
                    selectedPriority = provider.Priority;
                    conflict = false;
                    continue;
                }

                if (match == selectedMatch && provider.Priority == selectedPriority)
                {
                    conflict = true;
                }
            }

            if (conflict)
            {
                return new HybridInputProviderSelection(HybridInputSelectionStatus.Conflict, null);
            }

            return selected == null
                ? new HybridInputProviderSelection(HybridInputSelectionStatus.NoMatch, null)
                : new HybridInputProviderSelection(HybridInputSelectionStatus.Selected, selected);
        }

        private HybridInputProviderSelection SelectOverride(HybridInputEnvironment environment, string providerId)
        {
            for (var i = 0; i < providers.Count; ++i)
            {
                var provider = providers[i].Provider;
                if (!string.Equals(provider.Id, providerId, StringComparison.Ordinal))
                {
                    continue;
                }

                return provider.Match(environment) == HybridInputProviderMatch.None
                    ? new HybridInputProviderSelection(HybridInputSelectionStatus.OverrideRejected, null)
                    : new HybridInputProviderSelection(HybridInputSelectionStatus.Selected, provider);
            }

            return new HybridInputProviderSelection(HybridInputSelectionStatus.OverrideMissing, null);
        }

        private readonly struct RegisteredProvider
        {
            internal RegisteredProvider(long entryId, IHybridInputProvider provider)
            {
                EntryId = entryId;
                Provider = provider;
            }

            internal long EntryId { get; }
            internal IHybridInputProvider Provider { get; }
        }
    }

    internal readonly struct HybridInputProviderHandle
    {
        internal HybridInputProviderHandle(int epoch, long entryId)
        {
            Epoch = epoch;
            EntryId = entryId;
        }

        internal int Epoch { get; }
        internal long EntryId { get; }
    }

    internal readonly struct HybridInputProviderSelection
    {
        internal HybridInputProviderSelection(HybridInputSelectionStatus status, IHybridInputProvider? provider)
        {
            Status = status;
            Provider = provider;
        }

        internal HybridInputSelectionStatus Status { get; }
        internal IHybridInputProvider? Provider { get; }
    }
}
