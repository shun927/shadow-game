using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoundedRectangleGraphic : MaskableGraphic
{
    [SerializeField] private float cornerRadius = 8f;
    [SerializeField] private int cornerSegments = 8;

    public float CornerRadius
    {
        get => cornerRadius;
        set
        {
            cornerRadius = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    public int CornerSegments
    {
        get => cornerSegments;
        set
        {
            cornerSegments = Mathf.Max(1, value);
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f);
        int segments = Mathf.Max(1, cornerSegments);

        List<Vector2> points = new List<Vector2>((segments + 1) * 4);
        AddCorner(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, segments);
        AddCorner(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, segments);
        AddCorner(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, segments);
        AddCorner(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, segments);

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = rect.center;
        vh.AddVert(vertex);

        for (int i = 0; i < points.Count; i++)
        {
            vertex.position = points[i];
            vh.AddVert(vertex);
        }

        for (int i = 0; i < points.Count; i++)
        {
            int next = i + 1;
            if (next >= points.Count)
                next = 0;

            vh.AddTriangle(0, i + 1, next + 1);
        }
    }

    private static void AddCorner(List<Vector2> points, Vector2 center, float radius, float startAngle, float endAngle, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
            points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }
}
