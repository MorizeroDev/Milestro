using System;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Util;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using UnityEngine;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Milestro/World Space Slim Text")]
    public class WorldSpaceSlimText : MonoBehaviour
    {
        private const string DefaultFontFamily = "Source Han Sans VF";
        private const string DefaultTexturePropertyName = "_MainTex";

        [SerializeField] private string m_text = "";
        [SerializeField] private string m_fontFamily = DefaultFontFamily;
        [SerializeField, Range(0, 1000)] private int m_fontWeight = FontWeight.Normal;
        [SerializeField] private float m_fontSize = 24f;
        [SerializeField] private Color m_color = Color.white;
        [SerializeField] private Vector2Int m_textureSizePixels = new Vector2Int(128, 32);
        [SerializeField] private float m_pixelsPerUnit = 100f;
        [SerializeField] private Vector2 m_padding = Vector2.zero;
        [SerializeField] private bool m_autoFitTexture;
        [SerializeField] private bool m_fallbackToSystemFont = true;
        [SerializeField] private Material? m_material;
        [SerializeField] private string m_texturePropertyName = DefaultTexturePropertyName;
        [SerializeField] private bool m_disableRendererWhenNoOutput;
        [SerializeField, HideInInspector] private SlimTextRenderTextureProducer? m_ownedProducer;
        [SerializeField, HideInInspector] private RenderTextureMeshPresenter? m_ownedPresenter;
        [SerializeField, HideInInspector] private MeshFilter? m_ownedMeshFilter;
        [SerializeField, HideInInspector] private MeshRenderer? m_ownedMeshRenderer;

        [NonSerialized] private SlimTextRenderTextureProducer? producerCache;
        [NonSerialized] private RenderTextureMeshPresenter? presenterCache;
        [NonSerialized] private RectTransform? rectTransformCache;
        [NonSerialized] private MeshFilter? meshFilterCache;
        [NonSerialized] private MeshRenderer? meshRendererCache;
        [NonSerialized] private Mesh? generatedMesh;
        [NonSerialized] private Material? generatedMaterial;
        [NonSerialized] private Vector2Int appliedTextureSizePixels;
        [NonSerialized] private float appliedPixelsPerUnit;

        public string text
        {
            get => m_text;
            set
            {
                m_text = value ?? "";
                ApplyConfiguration();
            }
        }

        public string fontFamily
        {
            get => m_fontFamily;
            set
            {
                m_fontFamily = value ?? "";
                ApplyConfiguration();
            }
        }

        public float fontSize
        {
            get => m_fontSize;
            set
            {
                m_fontSize = NormalizeFontSize(value);
                ApplyConfiguration();
            }
        }

        public int fontWeight
        {
            get => m_fontWeight;
            set
            {
                m_fontWeight = NormalizeFontWeight(value);
                ApplyConfiguration();
            }
        }

        public Color color
        {
            get => m_color;
            set
            {
                m_color = value;
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
                m_pixelsPerUnit = NormalizePixelsPerUnit(value);
                ApplyConfiguration();
            }
        }

        public Vector2 padding
        {
            get => m_padding;
            set
            {
                m_padding = NormalizePadding(value);
                ApplyConfiguration();
            }
        }

        public bool autoFitTexture
        {
            get => m_autoFitTexture;
            set
            {
                m_autoFitTexture = value;
                ApplyConfiguration();
            }
        }

        public bool fallbackToSystemFont
        {
            get => m_fallbackToSystemFont;
            set
            {
                m_fallbackToSystemFont = value;
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

        private void OnDisable()
        {
            if (m_ownedProducer != null)
            {
                m_ownedProducer.enabled = false;
            }
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
            var targetTextureSizePixels = CurrentTextureSizePixels();

            ApplyRectTransformSettings(rectTransform, targetTextureSizePixels);
            ApplyProducerSettings(producer);
            ApplyPresenterSettings(presenter, producer);
            ApplyMesh(meshFilter, targetTextureSizePixels);
            ApplyMaterial(meshRenderer);
        }

        private void ApplyRectTransformSettings(RectTransform rectTransform, Vector2Int textureSizePixels)
        {
            var nextSize = new Vector2(textureSizePixels.x, textureSizePixels.y);
            if (rectTransform.sizeDelta != nextSize)
            {
                rectTransform.sizeDelta = nextSize;
            }
        }

        private void ApplyProducerSettings(SlimTextRenderTextureProducer producer)
        {
            producer.text = m_text;
            producer.fontFamily = m_fontFamily;
            producer.fontWeight = m_fontWeight;
            producer.fontSize = m_fontSize;
            producer.color = m_color;
            producer.padding = m_padding;
            producer.fallbackToSystemFont = m_fallbackToSystemFont;
        }

        private void ApplyPresenterSettings(RenderTextureMeshPresenter presenter,
            SlimTextRenderTextureProducer producer)
        {
            presenter.Producer = producer;
            presenter.TexturePropertyName = string.IsNullOrEmpty(m_texturePropertyName)
                ? DefaultTexturePropertyName
                : m_texturePropertyName;
            presenter.DisableRendererWhenNoOutput = m_disableRendererWhenNoOutput;
        }

        private void ApplyMesh(MeshFilter meshFilter, Vector2Int textureSizePixels)
        {
            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = "Milestro World Slim Text Quad"
                };
                generatedMesh.MarkDynamic();
            }

            if (meshFilter.sharedMesh != generatedMesh)
            {
                meshFilter.sharedMesh = generatedMesh;
            }

            if (appliedTextureSizePixels == textureSizePixels &&
                Mathf.Approximately(appliedPixelsPerUnit, m_pixelsPerUnit))
            {
                return;
            }

            RebuildQuadMesh(generatedMesh, textureSizePixels);
            appliedTextureSizePixels = textureSizePixels;
            appliedPixelsPerUnit = m_pixelsPerUnit;
        }

        private void RebuildQuadMesh(Mesh mesh, Vector2Int textureSizePixels)
        {
            var width = textureSizePixels.x / m_pixelsPerUnit;
            var height = textureSizePixels.y / m_pixelsPerUnit;
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

            var resourceMaterial =
                Resources.Load<Material>(
                    MilestroConfiguration.Configuration.WorldSpaceTextBox.DefaultMaterialResourcePath);
            if (resourceMaterial != null)
            {
                generatedMaterial = new Material(resourceMaterial)
                {
                    name = resourceMaterial.name + " (WorldSpaceSlimText)"
                };
                EnsureVisibleMaterialTint(generatedMaterial);
                return generatedMaterial;
            }

            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            generatedMaterial = new Material(shader)
            {
                name = "Milestro World Slim Text Material"
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

        private SlimTextRenderTextureProducer ProducerComponent()
        {
            if (producerCache == null)
            {
                producerCache = GetComponent<SlimTextRenderTextureProducer>();
                if (producerCache == null)
                {
                    producerCache = gameObject.AddComponent<SlimTextRenderTextureProducer>();
                    m_ownedProducer = producerCache;
                    MarkWrapperDirty();
                }
            }

            if (producerCache == m_ownedProducer)
            {
                if (isActiveAndEnabled && !producerCache.enabled)
                {
                    producerCache.enabled = true;
                }

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
            var ownedProducer = m_ownedProducer;
            var ownedPresenter = m_ownedPresenter;
            var ownedMeshRenderer = m_ownedMeshRenderer;
            var ownedMeshFilter = m_ownedMeshFilter;

            if (producerCache == ownedProducer)
            {
                producerCache = null;
            }

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

            m_ownedProducer = null;
            m_ownedPresenter = null;
            m_ownedMeshRenderer = null;
            m_ownedMeshFilter = null;

            DestroyOwnedComponent(ownedPresenter);
            DestroyOwnedComponent(ownedProducer);
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

        private Vector2Int CurrentTextureSizePixels()
        {
            var manualSize = NormalizeTextureSize(m_textureSizePixels);
            if (!m_autoFitTexture || string.IsNullOrEmpty(m_text))
            {
                return manualSize;
            }

            try
            {
                using var font = FontRegistry.ResolveFont(m_fontFamily,
                    m_fontWeight,
                    m_fontSize,
                    m_fallbackToSystemFont);
                var measurement = font.MeasureText(m_text);
                var baselineX = m_padding.x - measurement.Bounds.xMin;
                var baselineY = m_padding.y - measurement.Bounds.yMin;
                var width = Mathf.CeilToInt(baselineX +
                                            Mathf.Max(measurement.Bounds.xMax, measurement.AdvanceX) +
                                            m_padding.x);
                var height = Mathf.CeilToInt(baselineY + measurement.Bounds.yMax + m_padding.y);
                return NormalizeTextureSize(new Vector2Int(width, height));
            }
            catch (Exception)
            {
                return manualSize;
            }
        }

        private void NormalizeSerializedValues()
        {
            if (m_text == null)
            {
                m_text = "";
            }

            if (m_fontFamily == null)
            {
                m_fontFamily = "";
            }

            m_fontWeight = NormalizeFontWeight(m_fontWeight);
            m_fontSize = NormalizeFontSize(m_fontSize);
            m_textureSizePixels = NormalizeTextureSize(m_textureSizePixels);
            m_pixelsPerUnit = NormalizePixelsPerUnit(m_pixelsPerUnit);
            m_padding = NormalizePadding(m_padding);
            if (string.IsNullOrEmpty(m_texturePropertyName))
            {
                m_texturePropertyName = DefaultTexturePropertyName;
            }
        }

        private static Vector2Int NormalizeTextureSize(Vector2Int sizePixels)
        {
            return new Vector2Int(Mathf.Max(1, sizePixels.x), Mathf.Max(1, sizePixels.y));
        }

        private static int NormalizeFontWeight(int weight)
        {
            return Mathf.Clamp(weight, FontWeight.Invisible, FontWeight.ExtraBlack);
        }

        private static float NormalizeFontSize(float fontSize)
        {
            return FloatUtil.IsFinite(fontSize) ? Mathf.Max(1f, fontSize) : 1f;
        }

        private static float NormalizePixelsPerUnit(float pixelsPerUnit)
        {
            return FloatUtil.IsFinite(pixelsPerUnit) ? Mathf.Max(1f, pixelsPerUnit) : 1f;
        }

        private static Vector2 NormalizePadding(Vector2 padding)
        {
            return new Vector2(FloatUtil.IsFinite(padding.x) ? Mathf.Max(0f, padding.x) : 0f,
                FloatUtil.IsFinite(padding.y) ? Mathf.Max(0f, padding.y) : 0f);
        }
    }
}
