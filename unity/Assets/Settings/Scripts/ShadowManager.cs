using System.Collections.Generic;
using OomiyaFes.Field;
using OomiyaFes.Input;
using UnityEngine;

public class ShadowManager : MonoBehaviour
{
    [Header("Shadow Circle")]
    [SerializeField] private Transform shadowCircle;
    [SerializeField] private float shadowRadius = 3f;
    [SerializeField] private float shadowZ = 0f;
    [SerializeField] private Camera mainCamera;

    [Header("AR Marker Input")]
    [SerializeField] private UdpTrackingReceiver trackingReceiver;
    [SerializeField] private bool useTrackingWhenAvailable = true;
    [SerializeField] private Transform fieldFrame;
    [SerializeField] private Vector2 framePhysicalSizeMeters = new Vector2(0.3f, 0.3f);
    [SerializeField] private float fieldWorldSize = 30f;
    [SerializeField] private Vector2 fieldWorldCenter = Vector2.zero;
    [SerializeField] private bool invertTrackingDirection;
    [SerializeField] private bool allowMouseLightFallback = true;

    [Header("Object Shadows")]
    [SerializeField] private Transform[] shadowCasters;
    [SerializeField] private float shadowLength = 12f;
    [SerializeField] private float lightHeightMeters = 0.2f;
    [SerializeField] private float minLightObjectHeightGapMeters = 0.02f;
    [SerializeField] private Color generatedShadowColor = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField] private int shadowSortingOrder = -8;
    [SerializeField] private float wallPadding = 0.08f;
    [SerializeField] private float wallSlideSkin = 0.01f;
    [SerializeField] private bool useGeneratedObjectShadows = true;

    [Header("Respawn")]
    [SerializeField] private Transform player;
    [SerializeField] private PointManager pointManager;
    [SerializeField] private float timeOutsideShadow = 2f;

    [Header("Fade Effect")]
    [SerializeField] private SpriteRenderer fadeTarget;

    [Header("Shadow Trail")]
    [SerializeField] private GameObject trailShadowPrefab;
    [SerializeField] private float trailDistance = 0.5f;
    [SerializeField] private float trailRadius = 0.5f;
    [SerializeField] private float trailOffsetBehind = 0.5f;
    [SerializeField] private int maxTrailShadows = 50;

    private float outsideTimer;
    private Color fadeOriginalColor;
    private float trailDistanceAccum;
    private Vector3 lastPlayerPos;
    private Vector3 lastSafePlayerPos;
    private readonly List<GameObject> trailShadows = new List<GameObject>();
    private readonly List<Vector2[]> currentShadows = new List<Vector2[]>();
    private int checkpointTrailCount;
    private Mesh shadowMesh;
    private MeshFilter shadowMeshFilter;
    private MeshRenderer shadowMeshRenderer;

    void Start()
    {
        if (trackingReceiver == null)
            trackingReceiver = FindFirstObjectByType<UdpTrackingReceiver>();

        if (fadeTarget != null)
            fadeOriginalColor = fadeTarget.color;
        if (player != null)
        {
            lastPlayerPos = player.position;
            lastSafePlayerPos = player.position;
        }

        EnsureShadowMesh();
    }

    void Update()
    {
        if (mainCamera == null || player == null) return;

        Vector3 lightWorld = GetLightWorldPosition();

        // ライト位置の目印を追従
        if (shadowCircle != null)
        {
            shadowCircle.position = new Vector3(lightWorld.x, lightWorld.y, shadowZ);
            shadowCircle.localScale = Vector3.one * Mathf.Max(0.15f, shadowRadius * 0.2f);
        }

        UpdateObjectShadows(lightWorld);
        ResolvePlayerWallCollision();

        // 移動中に影の軌跡を生成（プレイヤーの現在位置ではなく少し後ろに配置）
        GenerateTrail();

        // プレイヤーが影の範囲内にいるか判定
        bool inShadow = IsPlayerInAnyShadow(lightWorld);

        if (!inShadow)
        {
            outsideTimer += Time.deltaTime;

            // フェード: 残り時間に応じて透明に
            float t = Mathf.Clamp01(outsideTimer / timeOutsideShadow);
            float alpha = Mathf.Lerp(1f, 0f, t);
            
            if (player.TryGetComponent<Player>(out var playerScript))
            {
                playerScript.SetChildrenAlpha(alpha);
            }
            
            if (fadeTarget != null)
            {
                Color c = fadeOriginalColor;
                c.a = Mathf.Lerp(fadeOriginalColor.a, 0f, t);
                fadeTarget.color = c;
            }

            if (outsideTimer >= timeOutsideShadow)
            {
                Respawn();
                outsideTimer = 0f;
            }
        }
        else
        {
            outsideTimer = 0f;

            // 影の中に戻ったら色を復帰
            if (player.TryGetComponent<Player>(out var playerScript))
            {
                playerScript.ResetChildrenAlpha();
            }

            if (fadeTarget != null)
                fadeTarget.color = fadeOriginalColor;
        }

        lastPlayerPos = player.position;
        if (!IsPlayerInsideWall())
            lastSafePlayerPos = player.position;
    }

    private Vector3 GetLightWorldPosition()
    {
        if (useTrackingWhenAvailable && TryGetTrackedLightWorldPosition(out var trackedWorld))
            return trackedWorld;

        if (!allowMouseLightFallback && shadowCircle != null)
            return shadowCircle.position;

        Vector2 mouseScreen = UnityEngine.Input.mousePosition;
        return mainCamera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -mainCamera.transform.position.z));
    }

    private bool TryGetTrackedLightWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (trackingReceiver == null || !trackingReceiver.HasPose || !trackingReceiver.IsConnected)
            return false;

        var message = trackingReceiver.Latest;
        Vector3 fieldPosition = FieldCoordinateMapper.PythonToUnity(message.field_xyz_m);

        Vector2 fieldToWorld = GetFieldToWorldScale();
        Vector2 fieldCenter = GetFieldWorldCenter();
        worldPosition = new Vector3(
            fieldCenter.x + fieldPosition.x * fieldToWorld.x,
            fieldCenter.y + fieldPosition.z * fieldToWorld.y,
            shadowZ);
        return true;
    }

    private Vector2 GetFieldToWorldScale()
    {
        if (TryGetFrameBounds(out var bounds))
        {
            return new Vector2(
                bounds.size.x / Mathf.Max(0.001f, framePhysicalSizeMeters.x),
                bounds.size.y / Mathf.Max(0.001f, framePhysicalSizeMeters.y));
        }

        float fallbackScale = fieldWorldSize / FieldCoordinateMapper.FieldSizeMeters;
        return new Vector2(fallbackScale, fallbackScale);
    }

    private Vector2 GetFieldWorldCenter()
    {
        if (TryGetFrameBounds(out var bounds))
            return bounds.center;

        return fieldWorldCenter;
    }

    private bool TryGetFrameBounds(out Bounds bounds)
    {
        bounds = default;
        if (fieldFrame == null)
            return false;

        if (fieldFrame.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
        {
            Vector3 size = new Vector3(
                Mathf.Abs(spriteRenderer.size.x * fieldFrame.lossyScale.x),
                Mathf.Abs(spriteRenderer.size.y * fieldFrame.lossyScale.y),
                0.001f);
            bounds = new Bounds(fieldFrame.position, size);
            return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
        }

        if (fieldFrame.TryGetComponent<Renderer>(out var renderer))
        {
            bounds = renderer.bounds;
            return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
        }

        bounds = new Bounds(fieldFrame.position, fieldFrame.lossyScale);
        return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
    }

    private void EnsureShadowMesh()
    {
        if (shadowMeshFilter != null || !useGeneratedObjectShadows)
            return;

        var shadowObject = new GameObject("Generated Object Shadows");
        shadowMeshFilter = shadowObject.AddComponent<MeshFilter>();
        shadowMeshRenderer = shadowObject.AddComponent<MeshRenderer>();
        shadowMesh = new Mesh { name = "Generated Object Shadows Mesh" };
        shadowMeshFilter.sharedMesh = shadowMesh;

        var material = new Material(Shader.Find("Sprites/Default"));
        material.color = generatedShadowColor;
        shadowMeshRenderer.sharedMaterial = material;
        shadowMeshRenderer.sortingOrder = shadowSortingOrder;
    }

    private void UpdateObjectShadows(Vector3 lightWorld)
    {
        currentShadows.Clear();
        if (!useGeneratedObjectShadows)
            return;

        EnsureShadowMesh();
        if (shadowMesh == null)
            return;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var colors = new List<Color>();

        if (shadowCasters == null)
            return;

        for (int i = 0; i < shadowCasters.Length; i++)
        {
            if (shadowCasters[i] == null ||
                !TryGetCasterShadowData(shadowCasters[i], out var bounds, out var objectHeightWorld))
            {
                continue;
            }

            Vector2[] polygon = BuildHeightAwareShadowPolygon(bounds, objectHeightWorld, lightWorld);
            if (polygon.Length < 3)
                continue;

            currentShadows.Add(polygon);

            int start = vertices.Count;
            for (int p = 0; p < polygon.Length; p++)
            {
                vertices.Add(new Vector3(polygon[p].x, polygon[p].y, shadowZ));
                colors.Add(generatedShadowColor);
            }

            for (int p = 1; p < polygon.Length - 1; p++)
            {
                triangles.Add(start);
                triangles.Add(start + p);
                triangles.Add(start + p + 1);
            }
        }

        shadowMesh.Clear();
        shadowMesh.SetVertices(vertices);
        shadowMesh.SetTriangles(triangles, 0);
        shadowMesh.SetColors(colors);
        shadowMesh.RecalculateBounds();
    }

    private Vector2[] BuildHeightAwareShadowPolygon(Bounds bounds, float objectHeightWorld, Vector2 light)
    {
        Vector2[] bottomCorners = GetBoundsCorners(bounds);
        var candidates = new List<Vector2>(bottomCorners);

        float worldHeightScale = GetAverageFieldToWorldScale();
        float lightHeightWorld = Mathf.Max(0.001f, lightHeightMeters * worldHeightScale);
        float minGapWorld = Mathf.Max(0.001f, minLightObjectHeightGapMeters * worldHeightScale);
        float heightRatio = objectHeightWorld / Mathf.Max(minGapWorld, lightHeightWorld - objectHeightWorld);

        for (int i = 0; i < bottomCorners.Length; i++)
        {
            Vector2 fromLight = bottomCorners[i] - light;
            Vector2 projected = bottomCorners[i];
            if (fromLight.sqrMagnitude > 0.0001f)
            {
                Vector2 direction = fromLight.normalized;
                float heightExtraDistance = Mathf.Max(fromLight.magnitude * heightRatio, objectHeightWorld);
                heightExtraDistance = Mathf.Min(heightExtraDistance, shadowLength);
                projected = bottomCorners[i] + direction * heightExtraDistance;
            }
            candidates.Add(projected);
        }

        return ConvexHull(candidates);
    }

    private static Vector2[] GetBoundsCorners(Bounds bounds)
    {
        Vector2 min = bounds.min;
        Vector2 max = bounds.max;
        return new[]
        {
            new Vector2(min.x, min.y),
            new Vector2(min.x, max.y),
            new Vector2(max.x, max.y),
            new Vector2(max.x, min.y),
        };
    }

    private float GetAverageFieldToWorldScale()
    {
        Vector2 scale = GetFieldToWorldScale();
        return (Mathf.Abs(scale.x) + Mathf.Abs(scale.y)) * 0.5f;
    }

    private bool TryGetCasterBounds(Transform caster, out Bounds bounds)
    {
        if (caster.TryGetComponent<PhysicalShadowCaster>(out var physicalCaster) &&
            physicalCaster.TryGetWorldFootprintBounds(out bounds))
        {
            return true;
        }

        var renderer = caster.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            bounds = renderer.bounds;
            return true;
        }

        bounds = new Bounds(caster.position, Vector3.one * 0.5f);
        return true;
    }

    private bool TryGetCasterShadowData(Transform caster, out Bounds footprintBounds, out float objectHeightWorld)
    {
        objectHeightWorld = 0f;
        if (caster.TryGetComponent<PhysicalShadowCaster>(out var physicalCaster) &&
            physicalCaster.TryGetWorldShadowCaster(out footprintBounds, out objectHeightWorld))
        {
            return true;
        }

        return TryGetCasterBounds(caster, out footprintBounds);
    }

    private void ResolvePlayerWallCollision()
    {
        if (player == null)
            return;

        Vector3 current = player.position;
        if (!IsPointInsideWall(current))
            return;

        Vector3 previous = lastPlayerPos;
        Vector3 xOnly = new Vector3(current.x, previous.y, current.z);
        Vector3 yOnly = new Vector3(previous.x, current.y, current.z);

        bool xOnlyClear = !IsPointInsideWall(xOnly);
        bool yOnlyClear = !IsPointInsideWall(yOnly);

        if (xOnlyClear && yOnlyClear)
        {
            float removedY = Mathf.Abs(current.y - xOnly.y);
            float removedX = Mathf.Abs(current.x - yOnly.x);
            player.position = removedY <= removedX ? xOnly : yOnly;
            return;
        }

        if (xOnlyClear)
        {
            player.position = xOnly;
            return;
        }

        if (yOnlyClear)
        {
            player.position = yOnly;
            return;
        }

        if (TryPushOutOfWall(current, out var pushed))
        {
            player.position = pushed;
            return;
        }

        player.position = lastSafePlayerPos;
    }

    private bool IsPlayerInsideWall()
    {
        if (player == null)
            return false;

        return IsPointInsideWall(player.position);
    }

    private bool IsPointInsideWall(Vector3 point)
    {
        if (shadowCasters == null)
            return false;

        for (int i = 0; i < shadowCasters.Length; i++)
        {
            if (shadowCasters[i] == null || !TryGetCasterCollisionBounds(shadowCasters[i], out var bounds))
                continue;

            bounds.Expand(wallPadding * 2f);
            if (bounds.Contains(new Vector3(point.x, point.y, bounds.center.z)))
                return true;
        }

        return false;
    }

    private bool TryPushOutOfWall(Vector3 point, out Vector3 pushed)
    {
        pushed = point;
        bool foundWall = false;
        float bestDistance = float.MaxValue;

        if (shadowCasters == null)
            return false;

        for (int i = 0; i < shadowCasters.Length; i++)
        {
            if (shadowCasters[i] == null || !TryGetCasterCollisionBounds(shadowCasters[i], out var bounds))
                continue;

            bounds.Expand(wallPadding * 2f);
            if (!bounds.Contains(new Vector3(point.x, point.y, bounds.center.z)))
                continue;

            float left = Mathf.Abs(point.x - bounds.min.x);
            float right = Mathf.Abs(bounds.max.x - point.x);
            float bottom = Mathf.Abs(point.y - bounds.min.y);
            float top = Mathf.Abs(bounds.max.y - point.y);
            float nearest = Mathf.Min(left, right, bottom, top);

            if (nearest >= bestDistance)
                continue;

            bestDistance = nearest;
            foundWall = true;

            pushed = point;
            if (nearest == left)
                pushed.x = bounds.min.x - wallSlideSkin;
            else if (nearest == right)
                pushed.x = bounds.max.x + wallSlideSkin;
            else if (nearest == bottom)
                pushed.y = bounds.min.y - wallSlideSkin;
            else
                pushed.y = bounds.max.y + wallSlideSkin;
        }

        return foundWall && !IsPointInsideWall(pushed);
    }

    private bool TryGetCasterCollisionBounds(Transform caster, out Bounds bounds)
    {
        if (caster.TryGetComponent<PhysicalShadowCaster>(out var physicalCaster) &&
            physicalCaster.TryGetWorldCollisionBounds(out bounds))
        {
            return true;
        }

        return TryGetCasterBounds(caster, out bounds);
    }

    private void GenerateTrail()
    {
        if (trailShadowPrefab == null) return;

        Vector3 movement = player.position - lastPlayerPos;
        float dist = movement.magnitude;
        if (dist < 0.001f) return;

        trailDistanceAccum += dist;
        if (trailDistanceAccum < trailDistance) return;
        trailDistanceAccum = 0f;

        // プレイヤーの現在位置ではなく、進行方向の後ろ側に配置
        Vector3 dir = movement / dist;
        Vector3 trailPos = player.position - dir * trailOffsetBehind;
        trailPos.z = shadowZ;

        GameObject trail = Instantiate(trailShadowPrefab, trailPos, Quaternion.identity);
        trail.transform.localScale = Vector3.one * trailRadius * 2f;
        trailShadows.Add(trail);

        // 上限を超えたら古い影から削除（チェックポイント分は保護）
        while (trailShadows.Count > maxTrailShadows && checkpointTrailCount < trailShadows.Count)
        {
            if (checkpointTrailCount > 0)
            {
                // チェックポイント以降の最も古い影を削除
                if (trailShadows[checkpointTrailCount] != null)
                    Destroy(trailShadows[checkpointTrailCount]);
                trailShadows.RemoveAt(checkpointTrailCount);
            }
            else
            {
                // チェックポイントなし → 先頭から削除
                if (trailShadows[0] != null)
                    Destroy(trailShadows[0]);
                trailShadows.RemoveAt(0);
            }
        }
    }

    private bool IsPlayerInAnyShadow(Vector3 mouseWorld)
    {
        Vector2 playerPos2D = (Vector2)player.position;

        if (useGeneratedObjectShadows)
        {
            for (int i = 0; i < currentShadows.Count; i++)
            {
                if (IsPointInShadowPolygon(playerPos2D, currentShadows[i]))
                    return true;
            }
        }
        else
        {
            // 実験用の旧挙動：ライト位置の円をそのまま影として扱う
            Vector2 mousePos2D = new Vector2(mouseWorld.x, mouseWorld.y);
            if ((playerPos2D - mousePos2D).sqrMagnitude <= shadowRadius * shadowRadius)
                return true;
        }

        // 軌跡の影
        for (int i = trailShadows.Count - 1; i >= 0; i--)
        {
            if (trailShadows[i] == null) continue;
            Vector2 trailPos2D = (Vector2)trailShadows[i].transform.position;
            if ((playerPos2D - trailPos2D).sqrMagnitude <= trailRadius * trailRadius)
                return true;
        }

        return false;
    }

    private static bool IsPointInShadowPolygon(Vector2 point, Vector2[] polygon)
    {
        if (polygon == null || polygon.Length < 3)
            return false;

        bool hasPositive = false;
        bool hasNegative = false;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Length];
            float cross = Cross(b - a, point - a);
            if (cross > 0.0001f)
                hasPositive = true;
            else if (cross < -0.0001f)
                hasNegative = true;

            if (hasPositive && hasNegative)
                return false;
        }

        return true;
    }

    private static Vector2[] ConvexHull(List<Vector2> points)
    {
        if (points == null || points.Count < 3)
            return new Vector2[0];

        points.Sort((a, b) =>
        {
            int xCompare = a.x.CompareTo(b.x);
            return xCompare != 0 ? xCompare : a.y.CompareTo(b.y);
        });

        var hull = new List<Vector2>();
        for (int i = 0; i < points.Count; i++)
        {
            while (hull.Count >= 2 &&
                   Cross(hull[hull.Count - 1] - hull[hull.Count - 2], points[i] - hull[hull.Count - 1]) <= 0f)
            {
                hull.RemoveAt(hull.Count - 1);
            }
            hull.Add(points[i]);
        }

        int lowerCount = hull.Count;
        for (int i = points.Count - 2; i >= 0; i--)
        {
            while (hull.Count > lowerCount &&
                   Cross(hull[hull.Count - 1] - hull[hull.Count - 2], points[i] - hull[hull.Count - 1]) <= 0f)
            {
                hull.RemoveAt(hull.Count - 1);
            }
            hull.Add(points[i]);
        }

        if (hull.Count > 1)
            hull.RemoveAt(hull.Count - 1);

        return hull.ToArray();
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    /// <summary>
    /// ポイント獲得時に呼び出して、現在の影をセーブする
    /// </summary>
    public void SaveCheckpoint()
    {
        checkpointTrailCount = trailShadows.Count;
    }

    private void Respawn()
    {
        // リスポーン時に色を完全に戻す
        if (player.TryGetComponent<Player>(out var playerScript))
        {
            playerScript.ResetChildrenAlpha();
        }

        if (fadeTarget != null)
            fadeTarget.color = fadeOriginalColor;

        // チェックポイント（最後のポイント獲得時）以降の影を削除
        for (int i = trailShadows.Count - 1; i >= checkpointTrailCount; i--)
        {
            if (trailShadows[i] != null)
                Destroy(trailShadows[i]);
            trailShadows.RemoveAt(i);
        }

        if (pointManager != null)
        {
            player.position = pointManager.RespawnPoint;
        }
        else
        {
            player.position = new Vector3(0f, 0f, -2f);
        }
    }
}
