using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class CircleWipeOverlay : Graphic
{
    [SerializeField] private int segments = 96;

    private Vector2 holeCenterScreen;
    private float holeRadiusPixels;

    public void SetHole(Vector2 screenCenter, float radiusPixels)
    {
        holeCenterScreen = screenCenter;
        holeRadiusPixels = Mathf.Max(0f, radiusPixels);
        SetVerticesDirty();
    }

    public void SetSegments(int segmentCount)
    {
        segments = Mathf.Max(12, segmentCount);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            holeCenterScreen,
            null,
            out Vector2 localCenter);

        Rect rect = rectTransform.rect;
        float outerRadius = new Vector2(rect.width, rect.height).magnitude;
        float innerRadius = Mathf.Min(holeRadiusPixels, outerRadius);
        int count = Mathf.Max(12, segments);

        for (int i = 0; i < count; i++)
        {
            float angle = (Mathf.PI * 2f * i) / count;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            AddVertex(vh, localCenter + direction * innerRadius);
            AddVertex(vh, localCenter + direction * outerRadius);
        }

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            int inner = i * 2;
            int outer = inner + 1;
            int nextInner = next * 2;
            int nextOuter = nextInner + 1;

            vh.AddTriangle(inner, outer, nextInner);
            vh.AddTriangle(outer, nextOuter, nextInner);
        }
    }

    private void AddVertex(VertexHelper vh, Vector2 position)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = position;
        vh.AddVert(vertex);
    }
}
