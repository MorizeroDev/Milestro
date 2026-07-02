using System;
using System.Collections.Generic;
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;
using UnityEngine.Serialization;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [AddComponentMenu("Milestro/World Space Text Box")]
    public class WorldSpaceTextBox : MonoBehaviour
    {
        private const string DefaultMaterialResourcePath = "Milestro/TextBoxDefaultMeterial";
        private const string DefaultTexturePropertyName = "_MainTex";

        [TextArea(3, 10)]
        [SerializeField]
        [FormerlySerializedAs("content")]
        private string m_content = "";

        [SerializeField]
        [FormerlySerializedAs("margin")]
        private RectOffset m_margin = new RectOffset();

        [SerializeField]
        [FormerlySerializedAs("fontFamilies")]
        private List<string> m_fontFamilies = new List<string>() { "Source Han Sans VF" };

        [SerializeField]
        [FormerlySerializedAs("textAlign")]
        private TextAlign m_textAlign = TextAlign.Start;

        [SerializeField]
        private TextDirection m_textDirection = TextDirection.Ltr;

        [SerializeField]
        [FormerlySerializedAs("size")]
        private float m_size = 36;

        [SerializeField]
        [FormerlySerializedAs("color")]
        private Color m_textColor = Color.white;

        [SerializeField]
        [FormerlySerializedAs("locale")]
        private string m_locale = "zh-Hans";

        [SerializeField] private Vector2Int m_textureSizePixels = new Vector2Int(1024, 256);
        [SerializeField] private float m_pixelsPerUnit = 100f;
        [SerializeField] private Material? m_material;
        [SerializeField] private string m_texturePropertyName = DefaultTexturePropertyName;
        [SerializeField] private bool m_disableRendererWhenNoOutput;
        [SerializeField, HideInInspector] private TextBoxRenderTargetProducer? m_ownedProducer;
        [SerializeField, HideInInspector] private RenderTargetMeshPresenter? m_ownedPresenter;

        [NonSerialized] private TextBoxRenderTargetProducer? producerCache;
        [NonSerialized] private RenderTargetMeshPresenter? presenterCache;
        [NonSerialized] private RectTransform? rectTransformCache;
        [NonSerialized] private MeshFilter? meshFilterCache;
        [NonSerialized] private MeshRenderer? meshRendererCache;
        [NonSerialized] private Mesh? generatedMesh;
        [NonSerialized] private Material? generatedMaterial;
        [NonSerialized] private Vector2Int appliedTextureSizePixels;
        [NonSerialized] private float appliedPixelsPerUnit;

        public string content
        {
            get => m_content;
            set
            {
                m_content = value ?? "";
                ApplyConfiguration();
            }
        }

        public RectOffset margin
        {
            get => m_margin;
            set
            {
                m_margin = value ?? new RectOffset();
                ApplyConfiguration();
            }
        }

        public List<string> fontFamilies
        {
            get => m_fontFamilies;
            set
            {
                m_fontFamilies = value ?? new List<string>();
                ApplyConfiguration();
            }
        }

        public TextAlign textAlign
        {
            get => m_textAlign;
            set
            {
                m_textAlign = value;
                ApplyConfiguration();
            }
        }

        public TextDirection textDirection
        {
            get => m_textDirection;
            set
            {
                m_textDirection = value;
                ApplyConfiguration();
            }
        }

        public float size
        {
            get => m_size;
            set
            {
                m_size = Mathf.Max(1f, value);
                ApplyConfiguration();
            }
        }

        public Color textColor
        {
            get => m_textColor;
            set
            {
                m_textColor = value;
                ApplyConfiguration();
            }
        }

        public string locale
        {
            get => m_locale;
            set
            {
                m_locale = value ?? "";
                ApplyConfiguration();
            }
        }

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
            ApplyProducerSettings(producer);
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

        private void ApplyProducerSettings(TextBoxRenderTargetProducer producer)
        {
            producer.content = m_content;
            producer.margin = m_margin;
            producer.fontFamilies = m_fontFamilies;
            producer.textAlign = m_textAlign;
            producer.textDirection = m_textDirection;
            producer.size = m_size;
            producer.textColor = m_textColor;
            producer.locale = m_locale;
        }

        private void ApplyPresenterSettings(RenderTargetMeshPresenter presenter,
            TextBoxRenderTargetProducer producer)
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

        private TextBoxRenderTargetProducer ProducerComponent()
        {
            if (producerCache == null)
            {
                producerCache = GetComponent<TextBoxRenderTargetProducer>();
                if (producerCache == null)
                {
                    producerCache = gameObject.AddComponent<TextBoxRenderTargetProducer>();
                    m_ownedProducer = producerCache;
                }
            }

            if (producerCache == m_ownedProducer)
            {
                HideOwnedComponent(producerCache);
            }

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

        private RenderTargetMeshPresenter PresenterComponent()
        {
            if (presenterCache == null)
            {
                presenterCache = GetComponent<RenderTargetMeshPresenter>();
                if (presenterCache == null)
                {
                    presenterCache = gameObject.AddComponent<RenderTargetMeshPresenter>();
                    m_ownedPresenter = presenterCache;
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
            ownedComponent.hideFlags |= HideFlags.HideInInspector;
        }

        private MeshFilter MeshFilterComponent()
        {
            if (meshFilterCache == null)
            {
                meshFilterCache = GetComponent<MeshFilter>();
                if (meshFilterCache == null)
                {
                    meshFilterCache = gameObject.AddComponent<MeshFilter>();
                }
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
                }
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
            var ownedProducer = m_ownedProducer;

            if (presenterCache == ownedPresenter)
            {
                presenterCache = null;
            }

            if (producerCache == ownedProducer)
            {
                producerCache = null;
            }

            m_ownedPresenter = null;
            m_ownedProducer = null;

            DestroyOwnedComponent(ownedPresenter);
            DestroyOwnedComponent(ownedProducer);
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

        private void NormalizeSerializedValues()
        {
            m_margin ??= new RectOffset();
            m_fontFamilies ??= new List<string>();
            m_textureSizePixels = NormalizeTextureSize(m_textureSizePixels);
            m_pixelsPerUnit = Mathf.Max(1f, m_pixelsPerUnit);
            m_size = Mathf.Max(1f, m_size);
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
