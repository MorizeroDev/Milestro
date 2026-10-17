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
        private List<Observation> observations = new List<Observation>();
        private List<Observation> scratchObservations = new List<Observation>();
        private bool dirty = true;
        private bool rebuilding;
        private int lastFrame = -1;

        public int BatchedRendererCount { get; private set; }
        public int BatchedTextCount { get; private set; }

        public void Rebuild()
        {
            UpdateBatches(force: true);
        }

        private void OnEnable()
        {
            dirty = true;
            lastFrame = -1;
            Camera.onPreCull += BeforeCamera;
            RenderPipelineManager.beginContextRendering += BeforeRenderPipeline;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= BeforeCamera;
            RenderPipelineManager.beginContextRendering -= BeforeRenderPipeline;
            RestoreObservations(observations);
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
                dirty = true;
            }
        }

        private void OnValidate()
        {
            dirty = true;
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
            if (!force && lastFrame == Time.frameCount && !dirty)
            {
                DisableManagedOriginals(observations);
                return;
            }
            lastFrame = Time.frameCount;
            GetComponentsInChildren(true, presenters);
            scratchObservations.Clear();
            var changed = force || dirty || observations.Count != presenters.Count;
            var worldToLocal = transform.worldToLocalMatrix;
            for (var index = 0; index < presenters.Count; ++index)
            {
                var presenter = presenters[index];
                Observation? previous = index < observations.Count ? observations[index] : null;
                var current = Observation.Create(presenter, previous, worldToLocal);
                scratchObservations.Add(current);
                if (!changed && !current.Equals(previous!.Value))
                {
                    changed = true;
                }
            }
            if (!changed)
            {
                DisableManagedOriginals(scratchObservations);
                return;
            }

            dirty = false;
            rebuilding = true;
            try
            {
                RebuildGroups(scratchObservations);
            }
            finally
            {
                rebuilding = false;
            }
            var previousObservations = observations;
            observations = scratchObservations;
            scratchObservations = previousObservations;
        }

        private void RebuildGroups(List<Observation> currentObservations)
        {
            RestoreObservations(observations);
            foreach (var group in groups.Values)
            {
                group.Seen = false;
                group.Entries.Clear();
            }
            foreach (var observation in currentObservations)
            {
                if (!observation.Valid)
                {
                    continue;
                }
                if (!groups.TryGetValue(observation.Key, out var group))
                {
                    group = new Group(observation.Key);
                    groups.Add(observation.Key, group);
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
                EnsureGenerated(group);
                RebuildMesh(group);
                ++BatchedRendererCount;
                BatchedTextCount += group.Entries.Count;
            }
            foreach (var key in staleKeys)
            {
                groups.Remove(key);
            }

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

        private void DisableManagedOriginals(List<Observation> currentObservations)
        {
            foreach (var observation in currentObservations)
            {
                if (observation.Valid && groups.TryGetValue(observation.Key, out var group) &&
                    group.Entries.Count >= 2 && group.GeneratedRenderer != null)
                {
                    DisableOriginal(observation);
                }
            }
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
            mesh.GetUVs(0, combinedUvs);
            if (mesh.vertexCount == expectedVertexCount && combinedUvs.Count == expectedVertexCount)
            {
                var vertexOffset = 0;
                foreach (var entry in group.Entries)
                {
                    var uvRect = entry.UvRect;
                    var vertexCount = entry.Mesh!.vertexCount;
                    for (var index = 0; index < vertexCount; ++index)
                    {
                        var uv = combinedUvs[vertexOffset + index];
                        combinedUvs[vertexOffset + index] = new Vector2(
                            uv.x * uvRect.width + uvRect.x,
                            uv.y * uvRect.height + uvRect.y);
                    }
                    vertexOffset += vertexCount;
                }
                mesh.SetUVs(0, combinedUvs);
            }
            mesh.RecalculateBounds();
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
        }

        private void DisableOriginal(Observation observation)
        {
            var targetRenderer = observation.Renderer;
            if (targetRenderer == null)
            {
                return;
            }
            if (!originalRendererStates.ContainsKey(targetRenderer))
            {
                originalRendererStates.Add(targetRenderer, targetRenderer.enabled);
            }
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
            internal bool Seen;
            internal MeshFilter? GeneratedFilter;
            internal MeshRenderer? GeneratedRenderer;
            internal Mesh? Mesh;

            internal Group(GroupKey key)
            {
                Key = key;
            }
        }

        private readonly struct Observation
        {
            internal readonly RenderTextureMeshPresenter Presenter;
            internal readonly RenderTextureProducer? Producer;
            internal readonly MeshFilter? Filter;
            internal readonly MeshRenderer? Renderer;
            internal readonly Mesh? Mesh;
            internal readonly Rect UvRect;
            internal readonly Matrix4x4 RelativeTransform;
            internal readonly Bounds MeshBounds;
            internal readonly GroupKey Key;
            internal readonly bool Active;
            internal readonly bool HasOutput;
            internal readonly bool Valid;

            private Observation(RenderTextureMeshPresenter presenter,
                RenderTextureProducer? producer,
                MeshFilter? filter,
                MeshRenderer? renderer,
                Mesh? mesh,
                Rect uvRect,
                Matrix4x4 relativeTransform,
                Bounds meshBounds,
                GroupKey key,
                bool active,
                bool hasOutput,
                bool valid)
            {
                Presenter = presenter;
                Producer = producer;
                Filter = filter;
                Renderer = renderer;
                Mesh = mesh;
                UvRect = uvRect;
                RelativeTransform = relativeTransform;
                MeshBounds = meshBounds;
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
                var valid = active && hasOutput && filter != null && targetRenderer != null &&
                            mesh != null && material != null && mesh.subMeshCount == 1;
                var key = GroupKey.Create(material, texture, presenter.TexturePropertyName,
                    presenter.gameObject.layer, targetRenderer);
                return new Observation(presenter, producer, filter, targetRenderer, mesh, uvRect,
                    worldToLocal * presenter.transform.localToWorldMatrix, meshBounds, key,
                    active, hasOutput, valid);
            }

            internal bool Equals(Observation other)
            {
                return ReferenceEquals(Presenter, other.Presenter) &&
                       ReferenceEquals(Producer, other.Producer) &&
                       ReferenceEquals(Filter, other.Filter) &&
                       ReferenceEquals(Renderer, other.Renderer) &&
                       ReferenceEquals(Mesh, other.Mesh) &&
                       UvRect.Equals(other.UvRect) &&
                       RelativeTransform.Equals(other.RelativeTransform) &&
                       MeshBounds.Equals(other.MeshBounds) &&
                       Key.Equals(other.Key) &&
                       Active == other.Active &&
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
