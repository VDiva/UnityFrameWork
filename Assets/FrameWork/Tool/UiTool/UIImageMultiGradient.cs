using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FrameWork
{
    /// <summary>为 UI Image 提供最多八个颜色节点、可旋转角度的线性渐变。</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class UIImageMultiGradient : MonoBehaviour, IMeshModifier
    {
        private const int MaxStops = 8;
        private static readonly int GradientCountId = Shader.PropertyToID("_GradientCount");
        private static readonly int GradientDirectionId = Shader.PropertyToID("_GradientDirection");
        private static readonly int[] GradientColorIds = CreatePropertyIds("_GradientColor");
        private static readonly int[] GradientPositionIds = CreatePropertyIds("_GradientPosition");

        [Serializable]
        public struct GradientStop
        {
            public Color color;

            public GradientStop(Color color)
            {
                this.color = color;
            }
        }

        [SerializeField] private Shader gradientShader;
        [SerializeField, Range(-360f, 360f)] private float angle = -90f;
        [SerializeField] private List<GradientStop> stops = new()
        {
            new GradientStop(Color.white),
            new GradientStop(Color.black)
        };

        private Image targetImage;
        private Material originalMaterial;
        private Material runtimeMaterial;
        private int lastAppliedStateHash;

        public float Angle
        {
            get => angle;
            set
            {
                angle = value;
                Apply();
            }
        }

        public IReadOnlyList<GradientStop> Stops => stops;

        private void OnEnable()
        {
            EnsureCanvasShaderChannel();
            EnsureMaterial();
            Apply();
        }

        private void OnDisable()
        {
            ReleaseMaterial();
        }

        private void OnDestroy()
        {
            ReleaseMaterial();
        }

        private void OnValidate()
        {
            EnsureStops();
            if (isActiveAndEnabled)
            {
                EnsureMaterial();
                Apply();
            }
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (!Application.isPlaying && isActiveAndEnabled &&
                CalculateStateHash() != lastAppliedStateHash)
            {
                Apply();
            }
        }
#endif

        private void OnDidApplyAnimationProperties()
        {
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && runtimeMaterial != null)
                Apply();
        }

        public void SetStops(IEnumerable<GradientStop> gradientStops)
        {
            stops.Clear();
            if (gradientStops != null)
                stops.AddRange(gradientStops);
            EnsureStops();
            Apply();
        }

        /// <summary>在运行时直接更新某个颜色节点。</summary>
        public void SetStop(int index, Color color)
        {
            if (index < 0 || index >= stops.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            stops[index] = new GradientStop(color);
            Apply();
        }

        /// <summary>把 Inspector 或运行时设置同步到当前 Image 的材质。</summary>
        public void Apply()
        {
            EnsureStops();
            EnsureMaterial();
            if (runtimeMaterial == null)
                return;

            int count = Mathf.Min(stops.Count, MaxStops);
            for (int index = 0; index < MaxStops; index++)
            {
                GradientStop stop = stops[Mathf.Min(index, count - 1)];
                runtimeMaterial.SetColor(GradientColorIds[index], stop.color);
                runtimeMaterial.SetFloat(GradientPositionIds[index],
                    count <= 1 ? 0f : Mathf.Min(index, count - 1) / (float)(count - 1));
            }

            float radians = angle * Mathf.Deg2Rad;
            runtimeMaterial.SetFloat(GradientCountId, count);
            runtimeMaterial.SetVector(GradientDirectionId,
                new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)));
            EnsureCanvasShaderChannel();
            targetImage.SetVerticesDirty();
            targetImage.SetMaterialDirty();
            lastAppliedStateHash = CalculateStateHash();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }

        /// <summary>
        /// 把 Image 局部矩形中的标准化坐标写入 UV1。
        /// Sprite 图集 UV 只用于采样图片，UV1 专门用于渐变，避免纵向和斜向渐变受图集影响。
        /// </summary>
        public void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!isActiveAndEnabled || vertexHelper == null || targetImage == null)
                return;

            Rect rect = targetImage.rectTransform.rect;
            if (Mathf.Approximately(rect.width, 0f) || Mathf.Approximately(rect.height, 0f))
                return;

            UIVertex vertex = default;
            for (int index = 0; index < vertexHelper.currentVertCount; index++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, index);
                vertex.uv1 = new Vector4(
                    Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x),
                    Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y),
                    0f,
                    0f);
                vertexHelper.SetUIVertex(vertex, index);
            }
        }

        [Obsolete("请使用 VertexHelper 版本。")]
        public void ModifyMesh(Mesh mesh)
        {
            if (mesh == null)
                return;

            using (var vertexHelper = new VertexHelper(mesh))
            {
                ModifyMesh(vertexHelper);
                vertexHelper.FillMesh(mesh);
            }
        }

        private void EnsureCanvasShaderChannel()
        {
            targetImage ??= GetComponent<Image>();
            Canvas canvas = targetImage.canvas;
            if (canvas != null)
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        private void EnsureStops()
        {
            stops ??= new List<GradientStop>();
            if (stops.Count == 0)
                stops.Add(new GradientStop(Color.white));
            if (stops.Count == 1)
                stops.Add(new GradientStop(stops[0].color));
            if (stops.Count > MaxStops)
                stops.RemoveRange(MaxStops, stops.Count - MaxStops);
        }

        private static int[] CreatePropertyIds(string prefix)
        {
            var ids = new int[MaxStops];
            for (int index = 0; index < ids.Length; index++)
                ids[index] = Shader.PropertyToID(prefix + index);
            return ids;
        }

        private int CalculateStateHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + angle.GetHashCode();
                hash = hash * 31 + (gradientShader == null ? 0 : gradientShader.GetInstanceID());
                hash = hash * 31 + (stops?.Count ?? 0);
                if (stops != null)
                {
                    foreach (GradientStop stop in stops)
                    {
                        hash = hash * 31 + stop.color.GetHashCode();
                    }
                }

                Rect rect = targetImage == null ? default : targetImage.rectTransform.rect;
                hash = hash * 31 + rect.GetHashCode();
                return hash;
            }
        }

        private void EnsureMaterial()
        {
            targetImage ??= GetComponent<Image>();
            if (runtimeMaterial != null)
                return;

            gradientShader ??= Shader.Find("UI/Multi Gradient");
            if (gradientShader == null)
            {
                Debug.LogError("找不到 Shader: UI/Multi Gradient", this);
                return;
            }

            originalMaterial = targetImage.material;
            runtimeMaterial = new Material(gradientShader)
            {
                name = $"{name} UI Multi Gradient (Instance)",
                hideFlags = HideFlags.HideAndDontSave
            };
            targetImage.material = runtimeMaterial;
        }

        private void ReleaseMaterial()
        {
            if (targetImage != null && targetImage.material == runtimeMaterial)
                targetImage.material = originalMaterial;
            if (runtimeMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);
            runtimeMaterial = null;
        }
    }
}
