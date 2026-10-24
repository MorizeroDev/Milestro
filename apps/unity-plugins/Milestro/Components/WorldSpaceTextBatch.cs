using System;
using System.Collections.Generic;
using Milestro.Components.Internal;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Milestro/World Space Text Batch")]
    public sealed class WorldSpaceTextBatch : MonoBehaviour
    {
        private static readonly Rect DefaultUvRect = new Rect(0f, 0f, 1f, 1f);

        private readonly List<RenderTextureMeshPresenter> presenters =
            new List<RenderTextureMeshPresenter>();
        private readonly Dictionary<GroupKey, Group> groups = new Dictionary<GroupKey, Group>();
        private readonly Dictionary<MeshRenderer, bool> originalRendererStates =
            new Dictionary<MeshRenderer, bool>();
        private readonly List<GroupKey> staleKeys = new List<GroupKey>();
        private readonly List<Vector2> combinedUvs = new List<Vector2>();
        private readonly HashSet<RenderTextureMeshPresenter> subscribedPresenters =
            new HashSet<RenderTextureMeshPresenter>();
        private readonly HashSet<RenderTextureMeshPresenter> presenterSetScratch =
            new HashSet<RenderTextureMeshPresenter>();
        private readonly List<RenderTextureMeshPresenter> stalePresenterSubscriptions =
            new List<RenderTextureMeshPresenter>();
        private readonly HashSet<RenderTextureMeshPresenter> changedPresenters =
            new HashSet<RenderTextureMeshPresenter>();
        private List<Observation> observations = new List<Observation>();
        private List<Observation> scratchObservations = new List<Observation>();
        private bool hierarchyDirty = true;
        private bool visualDirty = true;
        private bool rebuilding;
        private int lastFrame = -1;
        private int nextPresenterRescanFrame;
        private int nextFullObservationFrame;

        public int BatchedRendererCount { get; private set; }
        public int BatchedTextCount { get; private set; }

        public void Rebuild()
        {
            UpdateBatches(force: true);
        }

        private void OnEnable()
        {
            hierarchyDirty = true;
            visualDirty = true;
            lastFrame = -1;
            Camera.onPreCull += BeforeCamera;
            RenderPipelineManager.beginContextRendering += BeforeRenderPipeline;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= BeforeCamera;
            RenderPipelineManager.beginContextRendering -= BeforeRenderPipeline;
            RestoreObservations(observations);
            ClearPresenterSubscriptions();
            foreach (var group in groups.Values)
            {
                DestroyGenerated(group);
            }
            groups.Clear();
            foreach (var pair in originalRendererStates)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = pair.Value;
                }
            }
            originalRendererStates.Clear();
            presenters.Clear();
            observations.Clear();
            scratchObservations.Clear();
            BatchedRendererCount = 0;
            BatchedTextCount = 0;
        }

        private void OnTransformChildrenChanged()
        {
            if (!rebuilding)
            {
                hierarchyDirty = true;
            }
        }

        private void OnValidate()
        {
            hierarchyDirty = true;
        }

        private void BeforeCamera(Camera camera)
        {
            UpdateBatches(force: false);
        }

        private void BeforeRenderPipeline(ScriptableRenderContext context, List<Camera> cameras)
        {
            UpdateBatches(force: false);
        }

        internal void UpdateBatches(bool force)
        {
            var profiling = SlimTextPipelineProfiler.Enabled;
            using var profilerScope = profiling
                ? SlimTextPipelineProfiler.WorldTextBatchUpdateMarker.Auto()
                : default;
            var phaseStart = profiling ? SlimTextPipelineProfiler.Timestamp : 0;
            if (profiling)
            {
                SlimTextPipelineProfiler.WorldTextBatchUpdateCalls++;
            }

            try
            {
                UpdateBatchesCore(force);
            }
            finally
            {
                if (profiling)
                {
                    SlimTextPipelineProfiler.WorldTextBatchUpdateMs +=
                        SlimTextPipelineProfiler.ElapsedMs(phaseStart);
                }
            }
        }

        private void UpdateBatchesCore(bool force)
        {
            if (!force && lastFrame == Time.frameCount && !hierarchyDirty && !visualDirty)
            {
                DisableManagedOriginals(observations);
                return;
            }
            lastFrame = Time.frameCount;
            var needsPresenterRescan = force || hierarchyDirty || presenters.Count == 0 ||
                                       Time.frameCount >= nextPresenterRescanFrame;
            if (!needsPresenterRescan)
            {
                for (var index = 0; index < presenters.Count; ++index)
                {
                    if (presenters[index] == null)
                    {
                        needsPresenterRescan = true;
                        break;
                    }
                }
            }
            if (needsPresenterRescan)
            {
                GetComponentsInChildren(true, presenters);
                nextPresenterRescanFrame = Time.frameCount + 30;
                SyncPresenterSubscriptions();
            }
            var periodicObservationScan = Time.frameCount >= nextFullObservationFrame;
            if (!force && !hierarchyDirty && !visualDirty && !needsPresenterRescan &&
                !periodicObservationScan)
            {
                if (!PresenterTransformsChanged())
                {
                    return;
                }
                visualDirty = true;
            }
            var fullObservationScan = force || hierarchyDirty || needsPresenterRescan ||
                                      periodicObservationScan;
            if (fullObservationScan)
            {
                nextFullObservationFrame = Time.frameCount + 30;
            }
            scratchObservations.Clear();
            var changed = force || hierarchyDirty || observations.Count != presenters.Count;
            var worldToLocal = transform.worldToLocalMatrix;
            var profiling = SlimTextPipelineProfiler.Enabled;
            var observeStart = profiling ? SlimTextPipelineProfiler.Timestamp : 0;
            using var observeScope = profiling
                ? SlimTextPipelineProfiler.WorldTextBatchObserveMarker.Auto()
                : default;
            for (var index = 0; index < presenters.Count; ++index)
            {
                var presenter = presenters[index];
                Observation? previous = index < observations.Count ? observations[index] : null;
                Observation current;
                if (fullObservationScan || !previous.HasValue)
                {
                    current = Observation.Create(presenter, previous, worldToLocal);
                }
                else if (changedPresenters.Contains(presenter))
                {
                    current = previous.Value.Valid
                        ? Observation.Refresh(presenter, previous.Value, worldToLocal)
                        : Observation.Create(presenter, previous, worldToLocal);
                }
                else if (ReferenceEquals(presenter, previous.Value.Presenter) &&
                         !previous.Value.Transform.hasChanged)
                {
                    current = previous.Value;
                }
                else if (ReferenceEquals(presenter, previous.Value.Presenter) &&
                         (worldToLocal * previous.Value.Transform.localToWorldMatrix).Equals(
                             previous.Value.RelativeTransform))
                {
                    previous.Value.Transform.hasChanged = false;
                    current = previous.Value;
                }
                else if (previous.Value.Valid)
                {
                    previous.Value.Transform.hasChanged = false;
                    current = Observation.Refresh(presenter, previous.Value, worldToLocal);
                }
                else
                {
                    current = Observation.Create(presenter, previous, worldToLocal);
                }
                scratchObservations.Add(current);
                if (!changed && !current.VisualEquals(previous!.Value))
                {
                    changed = true;
                }
            }
            if (profiling)
            {
                SlimTextPipelineProfiler.WorldTextBatchObserveMs +=
                    SlimTextPipelineProfiler.ElapsedMs(observeStart);
            }
            changedPresenters.Clear();
            if (!changed)
            {
                if (fullObservationScan)
                {
                    DisableManagedOriginals(scratchObservations);
                }
                hierarchyDirty = false;
                visualDirty = false;
                var unusedObservations = observations;
                observations = scratchObservations;
                scratchObservations = unusedObservations;
                return;
            }

            hierarchyDirty = false;
            visualDirty = false;
            rebuilding = true;
            try
            {
                RebuildGroups(scratchObservations, force);
            }
            finally
            {
                rebuilding = false;
            }
            var previousObservations = observations;
            observations = scratchObservations;
            scratchObservations = previousObservations;
        }

        private void RebuildGroups(List<Observation> currentObservations, bool forceRebuild)
        {
            var profiling = SlimTextPipelineProfiler.Enabled;
            var stateStart = profiling ? SlimTextPipelineProfiler.Timestamp : 0;
            var samePresenters = observations.Count == currentObservations.Count;
            if (samePresenters)
            {
                for (var index = 0; index < observations.Count; ++index)
                {
                    if (!ReferenceEquals(observations[index].Presenter,
                            currentObservations[index].Presenter))
                    {
                        samePresenters = false;
                        break;
                    }
                }
            }
            if (!samePresenters)
            {
                using (profiling
                           ? SlimTextPipelineProfiler.WorldTextBatchOriginalStateMarker.Auto()
                           : default)
                {
                    RestoreObservations(observations);
                }
            }
            if (profiling)
            {
                SlimTextPipelineProfiler.WorldTextBatchOriginalStateMs +=
                    SlimTextPipelineProfiler.ElapsedMs(stateStart);
                stateStart = SlimTextPipelineProfiler.Timestamp;
            }
            foreach (var group in groups.Values)
            {
                group.Seen = false;
                group.Entries.Clear();
                group.PendingUpdate = GroupUpdate.None;
            }
            for (var index = 0; index < currentObservations.Count; ++index)
            {
                var observation = currentObservations[index];
                if (!observation.Valid)
                {
                    continue;
                }
                if (!groups.TryGetValue(observation.Key, out var group))
                {
                    group = new Group(observation.Key);
                    groups.Add(observation.Key, group);
                }
                var update = GroupUpdate.Mesh;
                if (!forceRebuild && index < observations.Count)
                {
                    var previous = observations[index];
                    if (observation.VisualEquals(previous))
                    {
                        update = GroupUpdate.None;
                    }
                    else if (group.GeneratedRenderer != null && observation.CanUpdateUvsOnly(previous))
                    {
                        update = GroupUpdate.Uvs;
                    }
                }
                if ((int)update > (int)group.PendingUpdate)
                {
                    group.PendingUpdate = update;
                }
                group.Seen = true;
                group.Entries.Add(observation);
            }

            BatchedRendererCount = 0;
            BatchedTextCount = 0;
            staleKeys.Clear();
            foreach (var pair in groups)
            {
                var group = pair.Value;
                if (!group.Seen)
                {
                    staleKeys.Add(pair.Key);
                    DestroyGenerated(group);
                    continue;
                }
                if (group.Entries.Count < 2)
                {
                    DestroyGenerated(group);
                    continue;
                }
                var hadGeneratedMesh = group.GeneratedRenderer != null;
                EnsureGenerated(group);
                if (!hadGeneratedMesh || group.PendingUpdate == GroupUpdate.Mesh)
                {
                    RebuildMesh(group);
                }
                else if (group.PendingUpdate == GroupUpdate.Uvs && !UpdateMeshUvs(group))
                {
                    RebuildMesh(group);
                }
                ++BatchedRendererCount;
                BatchedTextCount += group.Entries.Count;
            }
            foreach (var key in staleKeys)
            {
                groups.Remove(key);
            }

            using (profiling
                       ? SlimTextPipelineProfiler.WorldTextBatchOriginalStateMarker.Auto()
                       : default)
            {
                foreach (var observation in currentObservations)
                {
                    if (observation.Valid && groups.TryGetValue(observation.Key, out var group) &&
                        group.Entries.Count >= 2 && group.GeneratedRenderer != null)
                    {
                        DisableOriginal(observation);
                    }
                    else
                    {
                        RestoreOriginal(observation);
                    }
                }
            }
            if (profiling)
            {
                SlimTextPipelineProfiler.WorldTextBatchOriginalStateMs +=
                    SlimTextPipelineProfiler.ElapsedMs(stateStart);
            }
        }

        private void DisableManagedOriginals(List<Observation> currentObservations)
        {
            var profiling = SlimTextPipelineProfiler.Enabled;
            var phaseStart = profiling ? SlimTextPipelineProfiler.Timestamp : 0;
            using var profilerScope = profiling
                ? SlimTextPipelineProfiler.WorldTextBatchOriginalStateMarker.Auto()
                : default;
            foreach (var observation in currentObservations)
            {
                if (observation.Valid && groups.TryGetValue(observation.Key, out var group) &&
                    group.Entries.Count >= 2 && group.GeneratedRenderer != null)
                {
                    DisableOriginal(observation, verifyEnabled: true);
                }
            }
            if (profiling)
            {
                SlimTextPipelineProfiler.WorldTextBatchOriginalStateMs +=
                    SlimTextPipelineProfiler.ElapsedMs(phaseStart);
            }
        }

        private bool PresenterTransformsChanged()
        {
            if (observations.Count != presenters.Count)
            {
                return true;
            }

            var worldToLocal = transform.worldToLocalMatrix;
            for (var index = 0; index < presenters.Count; ++index)
            {
                var presenter = presenters[index];
                var previous = observations[index];
                if (presenter == null || !ReferenceEquals(presenter, previous.Presenter))
                {
                    return true;
                }

                if (!previous.Transform.hasChanged)
                {
                    continue;
                }

                previous.Transform.hasChanged = false;
                var relativeTransform = worldToLocal * previous.Transform.localToWorldMatrix;
                if (!relativeTransform.Equals(previous.RelativeTransform))
                {
                    return true;
                }
            }

            return false;
        }

        private void SyncPresenterSubscriptions()
        {
            presenterSetScratch.Clear();
            foreach (var presenter in presenters)
            {
                if (presenter == null || !presenterSetScratch.Add(presenter))
                {
                    continue;
                }

                if (subscribedPresenters.Add(presenter))
                {
                    presenter.VisualStateChanged += OnPresenterVisualStateChanged;
                }
            }

            stalePresenterSubscriptions.Clear();
            foreach (var presenter in subscribedPresenters)
            {
                if (!presenterSetScratch.Contains(presenter))
                {
                    stalePresenterSubscriptions.Add(presenter);
                }
            }
            foreach (var presenter in stalePresenterSubscriptions)
            {
                if (presenter != null)
                {
                    presenter.VisualStateChanged -= OnPresenterVisualStateChanged;
                }
                subscribedPresenters.Remove(presenter!);
            }
        }

        private void ClearPresenterSubscriptions()
        {
            foreach (var presenter in subscribedPresenters)
            {
                if (presenter != null)
                {
                    presenter.VisualStateChanged -= OnPresenterVisualStateChanged;
                }
            }
            subscribedPresenters.Clear();
            presenterSetScratch.Clear();
            stalePresenterSubscriptions.Clear();
            changedPresenters.Clear();
        }

        private void OnPresenterVisualStateChanged(RenderTextureMeshPresenter presenter)
        {
            visualDirty = true;
            changedPresenters.Add(presenter);
        }

        private void EnsureGenerated(Group group)
        {
            if (group.GeneratedRenderer != null)
            {
                return;
            }

            var key = group.Key;
            var generated = new GameObject("Milestro World Text Batch")
            {
                layer = key.Layer,
                hideFlags = HideFlags.HideAndDontSave
            };
            generated.transform.SetParent(transform, false);
            var filter = generated.AddComponent<MeshFilter>();
            var targetRenderer = generated.AddComponent<MeshRenderer>();
            var mesh = new Mesh
            {
                name = "Milestro World Text Batch Mesh",
                indexFormat = IndexFormat.UInt32
            };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            targetRenderer.sharedMaterial = key.Material;
            targetRenderer.shadowCastingMode = key.ShadowCastingMode;
            targetRenderer.receiveShadows = key.ReceiveShadows;
            targetRenderer.sortingLayerID = key.SortingLayerID;
            targetRenderer.sortingOrder = key.SortingOrder;
            var block = new MaterialPropertyBlock();
            block.SetTexture(Shader.PropertyToID(key.TexturePropertyName), key.Texture);
            block.SetVector(Shader.PropertyToID(key.TexturePropertyName + "_ST"),
                new Vector4(1f, 1f, 0f, 0f));
            targetRenderer.SetPropertyBlock(block);
            group.GeneratedFilter = filter;
            group.GeneratedRenderer = targetRenderer;
            group.Mesh = mesh;
        }

        private void RebuildMesh(Group group)
        {
            var profiling = SlimTextPipelineProfiler.Enabled;
            using var profilerScope = profiling
                ? SlimTextPipelineProfiler.WorldTextBatchMeshMarker.Auto()
                : default;
            var phaseStart = profiling ? SlimTextPipelineProfiler.Timestamp : 0;
            if (profiling)
            {
                SlimTextPipelineProfiler.WorldTextBatchMeshCalls++;
            }

            var mesh = group.Mesh!;
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            var instances = new CombineInstance[group.Entries.Count];
            var expectedVertexCount = 0;
            for (var index = 0; index < group.Entries.Count; ++index)
            {
                var entry = group.Entries[index];
                instances[index] = new CombineInstance
                {
                    mesh = entry.Mesh,
                    subMeshIndex = 0,
                    transform = entry.RelativeTransform
                };
                expectedVertexCount += entry.Mesh!.vertexCount;
            }
            mesh.CombineMeshes(instances, true, true, false);
            group.BaseUvs.Clear();
            mesh.GetUVs(0, group.BaseUvs);
            if (mesh.vertexCount == expectedVertexCount && group.BaseUvs.Count == expectedVertexCount)
            {
                ApplyGroupUvs(group);
            }
            mesh.RecalculateBounds();

            if (profiling)
            {
                SlimTextPipelineProfiler.WorldTextBatchMeshMs +=
                    SlimTextPipelineProfiler.ElapsedMs(phaseStart);
            }
        }

        private bool UpdateMeshUvs(Group group)
        {
            var profiling = SlimTextPipelineProfiler.Enabled;
            using var profilerScope = profiling
                ? SlimTextPipelineProfiler.WorldTextBatchUvMarker.Auto()
                : default;
            var phaseStart = profiling ? SlimTextPipelineProfiler.Timestamp : 0;
            if (profiling)
            {
                SlimTextPipelineProfiler.WorldTextBatchUvCalls++;
            }

            var updated = group.Mesh != null && ApplyGroupUvs(group);
            if (profiling)
            {
                SlimTextPipelineProfiler.WorldTextBatchUvMs +=
                    SlimTextPipelineProfiler.ElapsedMs(phaseStart);
            }

            return updated;
        }

        private bool ApplyGroupUvs(Group group)
        {
            var mesh = group.Mesh;
            if (mesh == null || group.BaseUvs.Count == 0 || mesh.vertexCount != group.BaseUvs.Count)
            {
                return false;
            }

            combinedUvs.Clear();
            combinedUvs.AddRange(group.BaseUvs);
            var vertexOffset = 0;
            foreach (var entry in group.Entries)
            {
                var uvRect = entry.UvRect;
                var vertexCount = entry.Mesh!.vertexCount;
                if (vertexOffset + vertexCount > combinedUvs.Count)
                {
                    return false;
                }
                for (var index = 0; index < vertexCount; ++index)
                {
                    var uv = combinedUvs[vertexOffset + index];
                    combinedUvs[vertexOffset + index] = new Vector2(
                        uv.x * uvRect.width + uvRect.x,
                        uv.y * uvRect.height + uvRect.y);
                }
                vertexOffset += vertexCount;
            }
            if (vertexOffset != combinedUvs.Count)
            {
                return false;
            }
            mesh.SetUVs(0, combinedUvs);
            return true;
        }

        private void DestroyGenerated(Group group)
        {
            if (group.GeneratedRenderer != null)
            {
                group.GeneratedRenderer.enabled = false;
            }
            if (group.GeneratedFilter != null)
            {
                group.GeneratedFilter.sharedMesh = null;
            }
            DestroyUnityObject(group.Mesh);
            DestroyUnityObject(group.GeneratedRenderer != null
                ? group.GeneratedRenderer.gameObject
                : null);
            group.GeneratedFilter = null;
            group.GeneratedRenderer = null;
            group.Mesh = null;
            group.BaseUvs.Clear();
        }

        private void DisableOriginal(Observation observation, bool verifyEnabled = false)
        {
            var targetRenderer = observation.Renderer;
            if (targetRenderer == null)
            {
                return;
            }
            if (originalRendererStates.ContainsKey(targetRenderer))
            {
                if (verifyEnabled && targetRenderer.enabled)
                {
                    targetRenderer.enabled = false;
                }
                return;
            }
            originalRendererStates.Add(targetRenderer, targetRenderer.enabled);
            targetRenderer.enabled = false;
        }

        private void RestoreObservations(List<Observation> source)
        {
            foreach (var observation in source)
            {
                RestoreOriginal(observation);
            }
        }

        private void RestoreOriginal(Observation observation)
        {
            var targetRenderer = observation.Renderer;
            if (targetRenderer == null ||
                !originalRendererStates.TryGetValue(targetRenderer, out var wasEnabled))
            {
                return;
            }
            var hasOutput = observation.Producer != null && observation.Producer.HasOutput;
            var outputAllowed = observation.Presenter == null ||
                                !observation.Presenter.DisableRendererWhenNoOutput || hasOutput;
            targetRenderer.enabled = wasEnabled && outputAllowed;
            originalRendererStates.Remove(targetRenderer);
        }

        private static void DestroyUnityObject(UnityEngine.Object? target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private sealed class Group
        {
            internal readonly GroupKey Key;
            internal readonly List<Observation> Entries = new List<Observation>();
            internal readonly List<Vector2> BaseUvs = new List<Vector2>();
            internal bool Seen;
            internal GroupUpdate PendingUpdate;
            internal MeshFilter? GeneratedFilter;
            internal MeshRenderer? GeneratedRenderer;
            internal Mesh? Mesh;

            internal Group(GroupKey key)
            {
                Key = key;
            }
        }

        private enum GroupUpdate
        {
            None,
            Uvs,
            Mesh
        }

        private readonly struct Observation
        {
            internal readonly RenderTextureMeshPresenter Presenter;
            internal readonly Transform Transform;
            internal readonly RenderTextureProducer? Producer;
            internal readonly MeshFilter? Filter;
            internal readonly MeshRenderer? Renderer;
            internal readonly Mesh? Mesh;
            internal readonly Rect UvRect;
            internal readonly Matrix4x4 LocalToWorld;
            internal readonly Matrix4x4 RelativeTransform;
            internal readonly Bounds MeshBounds;
            internal readonly int VertexCount;
            internal readonly long VisualVersion;
            internal readonly GroupKey Key;
            internal readonly bool Active;
            internal readonly bool HasOutput;
            internal readonly bool Valid;

            private Observation(RenderTextureMeshPresenter presenter,
                Transform transform,
                RenderTextureProducer? producer,
                MeshFilter? filter,
                MeshRenderer? renderer,
                Mesh? mesh,
                Rect uvRect,
                Matrix4x4 localToWorld,
                Matrix4x4 relativeTransform,
                Bounds meshBounds,
                int vertexCount,
                long visualVersion,
                GroupKey key,
                bool active,
                bool hasOutput,
                bool valid)
            {
                Presenter = presenter;
                Transform = transform;
                Producer = producer;
                Filter = filter;
                Renderer = renderer;
                Mesh = mesh;
                UvRect = uvRect;
                LocalToWorld = localToWorld;
                RelativeTransform = relativeTransform;
                MeshBounds = meshBounds;
                VertexCount = vertexCount;
                VisualVersion = visualVersion;
                Key = key;
                Active = active;
                HasOutput = hasOutput;
                Valid = valid;
            }

            internal static Observation Create(RenderTextureMeshPresenter presenter,
                Observation? previous,
                Matrix4x4 worldToLocal)
            {
                var filter = previous?.Filter;
                if (filter == null)
                {
                    filter = presenter.GetComponent<MeshFilter>();
                }
                var targetRenderer = previous?.Renderer;
                if (targetRenderer == null)
                {
                    targetRenderer = presenter.GetComponent<MeshRenderer>();
                }
                var producer = presenter.Producer;
                if (producer == null)
                {
                    producer = presenter.GetComponent<RenderTextureProducer>();
                }

                var active = presenter.isActiveAndEnabled;
                var texture = producer != null && producer.HasOutput ? producer.OutputTexture : null;
                var hasOutput = texture != null;
                var uvRect = hasOutput ? producer!.OutputUvRect : DefaultUvRect;
                var mesh = filter != null ? filter.sharedMesh : null;
                var material = targetRenderer != null ? targetRenderer.sharedMaterial : null;
                var meshBounds = mesh != null ? mesh.bounds : new Bounds();
                var vertexCount = mesh != null ? mesh.vertexCount : 0;
                var valid = active && hasOutput && filter != null && targetRenderer != null &&
                            mesh != null && material != null && mesh.subMeshCount == 1;
                var key = GroupKey.Create(material, texture, presenter.TexturePropertyName,
                    presenter.gameObject.layer, targetRenderer);
                var transform = presenter.transform;
                var localToWorld = transform.localToWorldMatrix;
                transform.hasChanged = false;
                var visualVersion = producer != null ? producer.VisualVersion : 0;
                return new Observation(presenter, transform, producer, filter, targetRenderer, mesh, uvRect,
                    localToWorld, worldToLocal * localToWorld, meshBounds, vertexCount,
                    visualVersion, key, active, hasOutput, valid);
            }

            internal static Observation Refresh(RenderTextureMeshPresenter presenter,
                Observation previous,
                Matrix4x4 worldToLocal)
            {
                var transform = presenter.transform;
                var localToWorld = transform.localToWorldMatrix;
                transform.hasChanged = false;
                var producer = presenter.Producer ?? previous.Producer;
                var active = presenter.isActiveAndEnabled;
                var texture = producer != null && producer.HasOutput ? producer.OutputTexture : null;
                var hasOutput = texture != null;
                var uvRect = hasOutput ? producer!.OutputUvRect : DefaultUvRect;
                var targetRenderer = previous.Renderer;
                var material = targetRenderer != null ? targetRenderer.sharedMaterial : null;
                var valid = active && hasOutput && previous.Filter != null && targetRenderer != null &&
                            previous.Mesh != null && material != null && previous.Mesh.subMeshCount == 1;
                var key = GroupKey.Create(material, texture, presenter.TexturePropertyName,
                    presenter.gameObject.layer, targetRenderer);
                var visualVersion = producer != null ? producer.VisualVersion : 0;
                return new Observation(presenter, transform, producer, previous.Filter, targetRenderer,
                    previous.Mesh, uvRect, localToWorld, worldToLocal * localToWorld,
                    previous.MeshBounds, previous.VertexCount, visualVersion, key, active,
                    hasOutput, valid);
            }

            internal bool VisualEquals(Observation other)
            {
                return ReferenceEquals(Presenter, other.Presenter) &&
                       ReferenceEquals(Producer, other.Producer) &&
                       ReferenceEquals(Filter, other.Filter) &&
                       ReferenceEquals(Renderer, other.Renderer) &&
                       ReferenceEquals(Mesh, other.Mesh) &&
                       UvRect.Equals(other.UvRect) &&
                       LocalToWorld.Equals(other.LocalToWorld) &&
                       RelativeTransform.Equals(other.RelativeTransform) &&
                       MeshBounds.Equals(other.MeshBounds) &&
                       VertexCount == other.VertexCount &&
                       VisualVersion == other.VisualVersion &&
                       Key.Equals(other.Key) &&
                       Active == other.Active &&
                       HasOutput == other.HasOutput &&
                       Valid == other.Valid;
            }

            internal bool CanUpdateUvsOnly(Observation other)
            {
                return !UvRect.Equals(other.UvRect) &&
                       TopologyEquals(other) &&
                       CanRemainInGroup(other);
            }

            private bool TopologyEquals(Observation other)
            {
                return ReferenceEquals(Presenter, other.Presenter) &&
                       ReferenceEquals(Producer, other.Producer) &&
                       ReferenceEquals(Filter, other.Filter) &&
                       ReferenceEquals(Renderer, other.Renderer) &&
                       ReferenceEquals(Mesh, other.Mesh) &&
                       LocalToWorld.Equals(other.LocalToWorld) &&
                       RelativeTransform.Equals(other.RelativeTransform) &&
                       MeshBounds.Equals(other.MeshBounds) &&
                       VertexCount == other.VertexCount &&
                       Key.Equals(other.Key);
            }

            private bool CanRemainInGroup(Observation other)
            {
                return Active == other.Active &&
                       HasOutput == other.HasOutput &&
                       Valid == other.Valid;
            }
        }

        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            internal readonly Material? Material;
            internal readonly Texture? Texture;
            internal readonly string TexturePropertyName;
            internal readonly int Layer;
            internal readonly int SortingLayerID;
            internal readonly int SortingOrder;
            internal readonly ShadowCastingMode ShadowCastingMode;
            internal readonly bool ReceiveShadows;

            private GroupKey(Material? material,
                Texture? texture,
                string texturePropertyName,
                int layer,
                int sortingLayerID,
                int sortingOrder,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows)
            {
                Material = material;
                Texture = texture;
                TexturePropertyName = texturePropertyName;
                Layer = layer;
                SortingLayerID = sortingLayerID;
                SortingOrder = sortingOrder;
                ShadowCastingMode = shadowCastingMode;
                ReceiveShadows = receiveShadows;
            }

            internal static GroupKey Create(Material? material,
                Texture? texture,
                string texturePropertyName,
                int layer,
                MeshRenderer? targetRenderer)
            {
                return new GroupKey(material, texture, texturePropertyName, layer,
                    targetRenderer != null ? targetRenderer.sortingLayerID : 0,
                    targetRenderer != null ? targetRenderer.sortingOrder : 0,
                    targetRenderer != null ? targetRenderer.shadowCastingMode : ShadowCastingMode.Off,
                    targetRenderer != null && targetRenderer.receiveShadows);
            }

            public bool Equals(GroupKey other)
            {
                return ReferenceEquals(Material, other.Material) &&
                       ReferenceEquals(Texture, other.Texture) &&
                       TexturePropertyName == other.TexturePropertyName &&
                       Layer == other.Layer &&
                       SortingLayerID == other.SortingLayerID &&
                       SortingOrder == other.SortingOrder &&
                       ShadowCastingMode == other.ShadowCastingMode &&
                       ReceiveShadows == other.ReceiveShadows;
            }

            public override bool Equals(object? obj)
            {
                return obj is GroupKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = Material != null ? Material.GetInstanceID() : 0;
                    hash = hash * 31 + (Texture != null ? Texture.GetInstanceID() : 0);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(TexturePropertyName ?? "");
                    hash = hash * 31 + Layer;
                    hash = hash * 31 + SortingLayerID;
                    hash = hash * 31 + SortingOrder;
                    hash = hash * 31 + (int)ShadowCastingMode;
                    hash = hash * 31 + (ReceiveShadows ? 1 : 0);
                    return hash;
                }
            }
        }
    }
}
