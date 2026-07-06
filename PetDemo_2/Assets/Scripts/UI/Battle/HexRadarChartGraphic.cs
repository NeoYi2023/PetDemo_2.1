// SPEC §12.13.2：六宫雷达图绘制（动态比例多边形 + 描边 + 参考六边形）。
using PetDemo.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Battle
{
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HexRadarChartGraphic : Graphic
    {
        [SerializeField] private float baseRadius = 180f;
        [SerializeField] private float lineWidth = 4f;
        [SerializeField] private Color fillColor = new Color(0.2f, 0.75f, 1f, 0.45f);
        [SerializeField] private Color lineColor = new Color(0.35f, 0.95f, 1f, 1f);
        [SerializeField] private Color guideColor = new Color(1f, 1f, 1f, 0.28f);

        private readonly float[] values = new float[AttrEnhanceConfigCatalog.HexRadarAttrIds.Length];

        protected override void Awake()
        {
            EnsureCanvasRenderer();
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetVerticesDirty();
        }

        public float BaseRadius
        {
            get => baseRadius;
            set
            {
                if (Mathf.Approximately(baseRadius, value))
                    return;
                baseRadius = value;
                SetVerticesDirty();
            }
        }

        public void SetValues(float[] source)
        {
            int n = values.Length;
            for (int i = 0; i < n; i++)
                values[i] = source != null && i < source.Length ? Mathf.Max(0f, source[i]) : 0f;
            SetVerticesDirty();
        }

        public void SetValuesFromInts(int[] source)
        {
            int n = values.Length;
            for (int i = 0; i < n; i++)
                values[i] = source != null && i < source.Length ? Mathf.Max(0, source[i]) : 0f;
            SetVerticesDirty();
        }

        /// <summary>确保预制体/运行时节点具备 CanvasRenderer（无则 Graphic 不会绘制）。</summary>
        public static void EnsureCanvasRenderer(GameObject go)
        {
            if (go == null)
                return;
            if (go.GetComponent<CanvasRenderer>() == null)
                go.AddComponent<CanvasRenderer>();
        }

        private void EnsureCanvasRenderer()
        {
            EnsureCanvasRenderer(gameObject);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float radius = ResolveDrawRadius();
            Vector2 center = Vector2.zero;

            DrawGuideHex(vh, center, radius);

            float maxVal = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] > maxVal)
                    maxVal = values[i];
            }

            if (maxVal <= 0f)
                return;

            var verts = new Vector2[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float r = values[i] / maxVal * radius;
                float rad = i * 60f * Mathf.Deg2Rad;
                verts[i] = center + new Vector2(Mathf.Sin(rad) * r, Mathf.Cos(rad) * r);
            }

            AddFilledPolygon(vh, center, verts, fillColor);
            AddPolyline(vh, verts, true, lineColor, lineWidth);
        }

        private float ResolveDrawRadius()
        {
            var rect = rectTransform.rect;
            if (rect.width > 1f && rect.height > 1f)
                return Mathf.Min(baseRadius, Mathf.Min(rect.width, rect.height) * 0.42f);
            return baseRadius;
        }

        private void DrawGuideHex(VertexHelper vh, Vector2 center, float radius)
        {
            var guide = new Vector2[values.Length];
            for (int i = 0; i < guide.Length; i++)
            {
                float rad = i * 60f * Mathf.Deg2Rad;
                guide[i] = center + new Vector2(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius);
            }
            AddPolyline(vh, guide, true, guideColor, Mathf.Max(2f, lineWidth * 0.75f));
        }

        private static void AddFilledPolygon(VertexHelper vh, Vector2 center, Vector2[] verts, Color color)
        {
            if (verts == null || verts.Length < 3)
                return;

            int centerIdx = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < verts.Length; i++)
                vh.AddVert(verts[i], color, Vector2.zero);

            for (int i = 0; i < verts.Length; i++)
            {
                int next = (i + 1) % verts.Length;
                vh.AddTriangle(centerIdx, centerIdx + 1 + i, centerIdx + 1 + next);
            }
        }

        private static void AddPolyline(VertexHelper vh, Vector2[] verts, bool closed, Color color, float width)
        {
            if (verts == null || verts.Length < 2)
                return;

            int count = closed ? verts.Length : verts.Length - 1;
            float half = width * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = verts[i];
                Vector2 b = verts[(i + 1) % verts.Length];
                Vector2 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.001f)
                    continue;
                Vector2 n = new Vector2(-dir.y, dir.x) / len * half;
                int idx = vh.currentVertCount;
                vh.AddVert(a - n, color, Vector2.zero);
                vh.AddVert(a + n, color, Vector2.zero);
                vh.AddVert(b + n, color, Vector2.zero);
                vh.AddVert(b - n, color, Vector2.zero);
                vh.AddTriangle(idx, idx + 1, idx + 2);
                vh.AddTriangle(idx, idx + 2, idx + 3);
            }
        }
    }
}
