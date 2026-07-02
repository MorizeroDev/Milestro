using System;
using Milestro.Components.Internal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using UnityEngine;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Milestro/World Space Text Box")]
    public class WorldSpaceTextBox : MonoBehaviour
    {
        private const string DefaultMaterialResourcePath = "Milestro/TextBoxDefaultMeterial";
        private const string DefaultTexturePropertyName = "_MainTex";

        [SerializeField] private Vector2Int m_textureSizePixels = new Vector2Int(1024, 256);
        [SerializeField] private float m_pixelsPerUnit = 100f;
        [SerializeField] private Material? m_material;
        [SerializeField] private string m_texturePropertyName = DefaultTexturePropertyName;
        [SerializeField] private bool m_disableRendererWhenNoOutput;
        [SerializeField, HideInInspector] private RenderTextureMeshPresenter? m_ownedPresenter;
        [SerializeField, HideInInspector] private MeshFilter? m_ownedMeshFilter;
        [SerializeField, HideInInspector] private MeshRenderer? m_ownedMeshRenderer;

        [NonSerialized] private TextBoxRenderTextureProducer? producerCache;
        [NonSerialized] private RenderTextureMeshPresenter? presenterCache;
        [NonSerialized] private RectTransform? rectTransformCache;
        [NonSerialized] private MeshFilter? meshFilterCache;
        [NonSerialized] private MeshRenderer? meshRendererCache;
        [NonSerialized] private Mesh? generatedMesh;
        [NonSerialized] private Material? generatedMaterial;
        [NonSerialized] private Vector2Int appliedTextureSizePixels;
        [NonSerialized] private float appliedPixelsPerUnit;

        public Vector2Int textureSizePixels
        {
            get => m_textureSizePixels;
            set
            {
                m_textureSizePixels = NormalizeTextureSize(value);
                ApplyConfiguration();
            }
        }

        public float pixelsPerUnit
        {
            get => m_pixelsPerUnit;
            set
            {
                m_pixelsPerUnit = Mathf.Max(1f, value);
                ApplyConfiguration();
            }
        }

        public Material? material
        {
            get => m_material;
            set
            {
                m_material = value;
                ApplyConfiguration();
            }
        }

        private void Reset()
        {
            NormalizeSerializedValues();
            EnsureConfigured();
        }

        private void OnEnable()
        {
            NormalizeSerializedValues();
            EnsureConfigured();
        }

        private void OnDestroy()
        {
            DestroyOwnedInternals();
            ReleaseGeneratedResources();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            NormalizeSerializedValues();
            if (this && gameObject != null)
            {
                EnsureConfigured();
            }
        }
#endif

        private void ApplyConfiguration()
        {
            NormalizeSerializedValues();
            if (!this || gameObject == null)
            {
                return;
            }

            EnsureConfigured();
        }

        private void EnsureConfigured()
        {
            var rectTransform = RectTransformComponent();
            var producer = ProducerComponent();
            var presenter = PresenterComponent();
            var meshFilter = MeshFilterComponent();
            var meshRenderer = MeshRendererComponent();

            ApplyRectTransformSettings(rectTransform);
            ApplyPresenterSettings(presenter, producer);
            ApplyMesh(meshFilter);
            ApplyMaterial(meshRenderer);
        }

        private void ApplyRectTransformSettings(RectTransform rectTransform)
        {
            var nextSize = new Vector2(m_textureSizePixels.x, m_textureSizePixels.y);
            if (rectTransform.sizeDelta != nextSize)
            {
                rectTransform.sizeDelta = nextSize;
            }
        }

        private void ApplyPresenterSettings(RenderTextureMeshPresenter presenter,
            TextBoxRenderTextureProducer producer)
        {
            presenter.Producer = producer;
            presenter.TexturePropertyName = string.IsNullOrEmpty(m_texturePropertyName)
                ? DefaultTexturePropertyName
                : m_texturePropertyName;
            presenter.DisableRendererWhenNoOutput = m_disableRendererWhenNoOutput;
        }

        private void ApplyMesh(MeshFilter meshFilter)
        {
            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = "Milestro World Text Box Quad"
                };
                generatedMesh.MarkDynamic();
            }

            if (meshFilter.sharedMesh != generatedMesh)
            {
                meshFilter.sharedMesh = generatedMesh;
            }

            if (appliedTextureSizePixels == m_textureSizePixels &&
                Mathf.Approximately(appliedPixelsPerUnit, m_pixelsPerUnit))
            {
                return;
            }

            RebuildQuadMesh(generatedMesh);
            appliedTextureSizePixels = m_textureSizePixels;
            appliedPixelsPerUnit = m_pixelsPerUnit;
        }

        private void RebuildQuadMesh(Mesh mesh)
        {
            var width = m_textureSizePixels.x / m_pixelsPerUnit;
            var height = m_textureSizePixels.y / m_pixelsPerUnit;
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;

            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, -halfHeight, 0f),
                new Vector3(-halfWidth, halfHeight, 0f),
                new Vector3(halfWidth, halfHeight, 0f),
                new Vector3(halfWidth, -halfHeight, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            mesh.normals = new[]
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 };
            mesh.RecalculateBounds();
        }

        private void ApplyMaterial(MeshRenderer meshRenderer)
        {
            if (!meshRenderer.enabled)
            {
                meshRenderer.enabled = true;
            }

            var targetMaterial = m_material != null ? m_material : DefaultMaterial();
            if (meshRenderer.sharedMaterial != targetMaterial)
            {
                meshRenderer.sharedMaterial = targetMaterial;
            }
        }

        private Material DefaultMaterial()
        {
            if (generatedMaterial != null)
            {
                return generatedMaterial;
            }

            var resourceMaterial = Resources.Load<Material>(DefaultMaterialResourcePath);
            if (resourceMaterial != null)
            {
                generatedMaterial = new Material(resourceMaterial)
                {
                    name = resourceMaterial.name + " (WorldSpaceTextBox)"
                };
                EnsureVisibleMaterialTint(generatedMaterial);
                return generatedMaterial;
            }

            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            generatedMaterial = new Material(shader)
            {
                name = "Milestro World Text Box Material"
            };
            EnsureVisibleMaterialTint(generatedMaterial);
            return generatedMaterial;
        }

        private static void EnsureVisibleMaterialTint(Material targetMaterial)
        {
            if (targetMaterial.HasProperty("_Color"))
            {
                targetMaterial.color = Color.white;
            }
        }

        private TextBoxRenderTextureProducer ProducerComponent()
        {
            if (producerCache == null)
            {
                producerCache = GetComponent<TextBoxRenderTextureProducer>();
                if (producerCache == null)
                {
                    producerCache = gameObject.AddComponent<TextBoxRenderTextureProducer>();
                    MarkWrapperDirty();
                }
            }

            ShowConfigurationComponent(producerCache);

            return producerCache;
        }

        private RectTransform RectTransformComponent()
        {
            if (rectTransformCache == null)
            {
                rectTransformCache = GetComponent<RectTransform>();
                if (rectTransformCache == null)
                {
                    rectTransformCache = gameObject.AddComponent<RectTransform>();
                }
            }

            return rectTransformCache;
        }

        private RenderTextureMeshPresenter PresenterComponent()
        {
            if (presenterCache == null)
            {
                presenterCache = GetComponent<RenderTextureMeshPresenter>();
                if (presenterCache == null)
                {
                    var hadMeshRenderer = GetComponent<MeshRenderer>() != null;
                    presenterCache = gameObject.AddComponent<RenderTextureMeshPresenter>();
                    m_ownedPresenter = presenterCache;
                    if (!hadMeshRenderer)
                    {
                        m_ownedMeshRenderer = GetComponent<MeshRenderer>();
                        meshRendererCache = m_ownedMeshRenderer;
                    }

                    MarkWrapperDirty();
                }
            }

            if (presenterCache == m_ownedPresenter)
            {
                HideOwnedComponent(presenterCache);
            }

            return presenterCache;
        }

        private static void HideOwnedComponent(Component ownedComponent)
        {
            var nextHideFlags = ownedComponent.hideFlags | HideFlags.HideInInspector;
            if (ownedComponent.hideFlags == nextHideFlags)
            {
                return;
            }

            ownedComponent.hideFlags = nextHideFlags;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(ownedComponent);
                EditorApplication.delayCall += RepaintEditorViews;
            }
#endif
        }

        private static void ShowConfigurationComponent(Component configurationComponent)
        {
            var nextHideFlags = configurationComponent.hideFlags & ~HideFlags.HideInInspector;
            if (configurationComponent.hideFlags == nextHideFlags)
            {
                return;
            }

            configurationComponent.hideFlags = nextHideFlags;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(configurationComponent);
                EditorApplication.delayCall += RepaintEditorViews;
            }
#endif
        }

#if UNITY_EDITOR
        private static void RepaintEditorViews()
        {
            InternalEditorUtility.RepaintAllViews();
        }
#endif

        private MeshFilter MeshFilterComponent()
        {
            if (meshFilterCache == null)
            {
                meshFilterCache = GetComponent<MeshFilter>();
                if (meshFilterCache == null)
                {
                    meshFilterCache = gameObject.AddComponent<MeshFilter>();
                    m_ownedMeshFilter = meshFilterCache;
                    MarkWrapperDirty();
                }
            }

            if (meshFilterCache == m_ownedMeshFilter)
            {
                HideOwnedComponent(meshFilterCache);
            }

            return meshFilterCache;
        }

        private MeshRenderer MeshRendererComponent()
        {
            if (meshRendererCache == null)
            {
                meshRendererCache = GetComponent<MeshRenderer>();
                if (meshRendererCache == null)
                {
                    meshRendererCache = gameObject.AddComponent<MeshRenderer>();
                    m_ownedMeshRenderer = meshRendererCache;
                    MarkWrapperDirty();
                }
            }

            if (meshRendererCache == m_ownedMeshRenderer)
            {
                HideOwnedComponent(meshRendererCache);
            }

            return meshRendererCache;
        }

        private void ReleaseGeneratedResources()
        {
            DestroyUnityObject(generatedMesh);
            DestroyUnityObject(generatedMaterial);
            generatedMesh = null;
            generatedMaterial = null;
        }

        private void DestroyOwnedInternals()
        {
            var ownedPresenter = m_ownedPresenter;
            var ownedMeshRenderer = m_ownedMeshRenderer;
            var ownedMeshFilter = m_ownedMeshFilter;

            if (presenterCache == ownedPresenter)
            {
                presenterCache = null;
            }

            if (meshRendererCache == ownedMeshRenderer)
            {
                meshRendererCache = null;
            }

            if (meshFilterCache == ownedMeshFilter)
            {
                meshFilterCache = null;
            }

            m_ownedPresenter = null;
            m_ownedMeshRenderer = null;
            m_ownedMeshFilter = null;

            DestroyOwnedComponent(ownedPresenter);
            DestroyOwnedComponent(ownedMeshRenderer);
            DestroyOwnedComponent(ownedMeshFilter);
        }

        private static void DestroyOwnedComponent(Component? ownedComponent)
        {
            DestroyUnityObject(ownedComponent);
        }

        private static void DestroyUnityObject(UnityEngine.Object? targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(targetObject);
            }
            else
            {
                DestroyImmediate(targetObject);
            }
        }

        private void MarkWrapperDirty()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        private void NormalizeSerializedValues()
        {
            m_textureSizePixels = NormalizeTextureSize(m_textureSizePixels);
            m_pixelsPerUnit = Mathf.Max(1f, m_pixelsPerUnit);
            if (string.IsNullOrEmpty(m_texturePropertyName))
            {
                m_texturePropertyName = DefaultTexturePropertyName;
            }
        }

        private static Vector2Int NormalizeTextureSize(Vector2Int sizePixels)
        {
            return new Vector2Int(Mathf.Max(1, sizePixels.x), Mathf.Max(1, sizePixels.y));
        }
    }
}
