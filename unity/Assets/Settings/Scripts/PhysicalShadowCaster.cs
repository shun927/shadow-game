using System;
using OomiyaFes.Field;
using OomiyaFes.Input;
using UnityEngine;

[ExecuteAlways]
public class PhysicalShadowCaster : MonoBehaviour
{
    [SerializeField] private UdpTrackingReceiver trackingReceiver;
    [SerializeField] private int markerId = 1;
    [SerializeField] private bool useTrackingWhenAvailable = true;
    [SerializeField] private float trackingTimeoutSeconds = 0.5f;

    [Header("Physical Dimensions")]
    [SerializeField] private Vector3 physicalSizeMeters = new Vector3(0.05f, 0.08f, 0.10f);
    [SerializeField] private Vector2 collisionSizeMultiplier = Vector2.one;
    [SerializeField] private Vector2 collisionPaddingMeters;
    [SerializeField] private Vector2 markerToObjectCenterOffsetMeters;
    [SerializeField] private bool useMarkerYaw = true;
    [SerializeField] private float rotationOffsetDegrees;

    [Header("Field Mapping")]
    [SerializeField] private Transform fieldFrame;
    [SerializeField] private Vector2 framePhysicalSizeMeters = new Vector2(0.3f, 0.3f);
    [SerializeField] private float fieldWorldSize = 30f;
    [SerializeField] private Vector2 fieldWorldCenter = Vector2.zero;
    [SerializeField] private Vector2 manualFieldPositionMeters;
    [SerializeField] private float worldZ = -1f;
    [SerializeField] private bool placeCenterAboveFieldSurface = true;
    [SerializeField] private float footprintSurfaceLift = 0.02f;

    [Header("Editor Preview")]
    [SerializeField] private bool syncMovedPositionToManualFieldPosition = true;
    [SerializeField] private bool useRectangularFootprintVisual = true;
    [SerializeField] private Color footprintVisualColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color footprintBorderColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private float footprintBorderWidth = 0.08f;
    [SerializeField] private int footprintBorderSortingOrder = 20;
    [SerializeField] private Color footprintGizmoColor = new Color(0.1f, 0.85f, 1f, 0.35f);

    private Vector3 lastEditorPosition;
    private bool hasEditorPosition;
    private Transform footprintVisualRoot;
    private MeshFilter footprintMeshFilter;
    private MeshRenderer footprintMeshRenderer;
    private LineRenderer footprintBorderRenderer;

    private void OnEnable()
    {
        RememberEditorPosition();
    }

    private void Start()
    {
        if (trackingReceiver == null)
            trackingReceiver = FindFirstObjectByType<UdpTrackingReceiver>();

        ApplyManualPosition();
        ApplySize();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            ApplySize();
            if (syncMovedPositionToManualFieldPosition && HasMovedInEditor())
            {
                SyncManualPositionFromTransform();
                RememberEditorPosition();
            }
            return;
        }

        ApplySize();

        if (!useTrackingWhenAvailable || trackingReceiver == null || !trackingReceiver.IsConnected)
        {
            ApplyManualPosition();
            return;
        }

        if (!trackingReceiver.TryGetLatest(markerId, out var message) || IsStale(message))
        {
            ApplyManualPosition();
            return;
        }

        Vector2 fieldPosition = ToField2D(message.field_xyz_m) + markerToObjectCenterOffsetMeters;
        transform.position = FieldToWorld(fieldPosition);

        if (useMarkerYaw && message.marker_x_axis_field != null)
        {
            Vector2 xAxis = new Vector2(message.marker_x_axis_field.x, message.marker_x_axis_field.y);
            if (xAxis.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(xAxis.y, xAxis.x) * Mathf.Rad2Deg + rotationOffsetDegrees;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }

    private void ApplyManualPosition()
    {
        transform.position = FieldToWorld(manualFieldPositionMeters + markerToObjectCenterOffsetMeters);
        transform.rotation = Quaternion.Euler(0f, 0f, rotationOffsetDegrees);
    }

    private void ApplySize()
    {
        Vector2 fieldToWorld = GetFieldToWorldScale();
        transform.localScale = new Vector3(
            Mathf.Max(0.001f, physicalSizeMeters.x * fieldToWorld.x),
            Mathf.Max(0.001f, physicalSizeMeters.y * fieldToWorld.y),
            1f);

        UpdateRectangularFootprintVisual();
    }

    private Vector3 FieldToWorld(Vector2 fieldPosition)
    {
        Vector2 fieldToWorld = GetFieldToWorldScale();
        Vector2 fieldCenter = GetFieldWorldCenter();
        return new Vector3(
            fieldCenter.x + fieldPosition.x * fieldToWorld.x,
            fieldCenter.y + fieldPosition.y * fieldToWorld.y,
            GetObjectCenterWorldZ());
    }

    private static Vector2 ToField2D(Vec3 fieldPosition)
    {
        if (fieldPosition == null)
            return Vector2.zero;

        return new Vector2(fieldPosition.x, fieldPosition.y);
    }

    private bool IsStale(TrackedMarkerMessage message)
    {
        if (message == null || message.timestamp_ms <= 0)
            return true;

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return nowMs - message.timestamp_ms > trackingTimeoutSeconds * 1000f;
    }

    private void SyncManualPositionFromTransform()
    {
        Vector2 fieldToWorld = GetFieldToWorldScale();
        Vector2 fieldCenter = GetFieldWorldCenter();
        manualFieldPositionMeters = new Vector2(
            (transform.position.x - fieldCenter.x) / fieldToWorld.x,
            (transform.position.y - fieldCenter.y) / fieldToWorld.y) - markerToObjectCenterOffsetMeters;
    }

    private void OnValidate()
    {
        fieldWorldSize = Mathf.Max(0.001f, fieldWorldSize);
        framePhysicalSizeMeters.x = Mathf.Max(0.001f, framePhysicalSizeMeters.x);
        framePhysicalSizeMeters.y = Mathf.Max(0.001f, framePhysicalSizeMeters.y);
        trackingTimeoutSeconds = Mathf.Max(0.01f, trackingTimeoutSeconds);

        if (!Application.isPlaying)
        {
            ApplyManualPosition();
            ApplySize();
            RememberEditorPosition();
        }
    }

    private void OnDrawGizmos()
    {
        ApplySize();

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = Matrix4x4.TRS(
            new Vector3(transform.position.x, transform.position.y, GetFootprintWorldZ()),
            transform.rotation,
            transform.lossyScale);

        Gizmos.color = footprintGizmoColor;
        Gizmos.DrawCube(Vector3.zero, Vector3.one);

        Gizmos.color = new Color(
            footprintGizmoColor.r,
            footprintGizmoColor.g,
            footprintGizmoColor.b,
            Mathf.Clamp01(footprintGizmoColor.a + 0.45f));
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private bool HasMovedInEditor()
    {
        if (!hasEditorPosition)
        {
            RememberEditorPosition();
            return false;
        }

        return (transform.position - lastEditorPosition).sqrMagnitude > 0.000001f;
    }

    private void RememberEditorPosition()
    {
        if (Application.isPlaying)
            return;

        lastEditorPosition = transform.position;
        hasEditorPosition = true;
    }

    private Vector2 GetFieldToWorldScale()
    {
        if (TryGetFrameBounds(out var bounds))
        {
            return new Vector2(
                bounds.size.x / framePhysicalSizeMeters.x,
                bounds.size.y / framePhysicalSizeMeters.y);
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

    public bool TryGetWorldFootprintBounds(out Bounds bounds)
    {
        bounds = new Bounds(
            new Vector3(transform.position.x, transform.position.y, GetFootprintWorldZ()),
            new Vector3(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), 0.001f));
        return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
    }

    public bool TryGetWorldShadowCaster(out Bounds footprintBounds, out float heightWorld)
    {
        heightWorld = GetObjectHeightWorld();
        return TryGetWorldFootprintBounds(out footprintBounds);
    }

    private void UpdateRectangularFootprintVisual()
    {
        var spriteRenderer = GetComponent<SpriteRenderer>();

        if (!useRectangularFootprintVisual)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = true;
            SetFootprintVisualVisible(false);
            return;
        }

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        EnsureFootprintMesh();
        if (footprintMeshRenderer == null)
            return;

        SetFootprintVisualVisible(true);
        footprintMeshRenderer.sortingOrder = -6;
        footprintMeshRenderer.sharedMaterial.color = footprintVisualColor;

        if (footprintBorderRenderer != null)
        {
            footprintBorderRenderer.sortingOrder = footprintBorderSortingOrder;
            footprintBorderRenderer.startColor = footprintBorderColor;
            footprintBorderRenderer.endColor = footprintBorderColor;
            footprintBorderRenderer.startWidth = footprintBorderWidth;
            footprintBorderRenderer.endWidth = footprintBorderWidth;
        }
    }

    private void EnsureFootprintMesh()
    {
        if (footprintVisualRoot == null)
        {
            Transform existing = transform.Find("Rectangular Footprint Visual");
            if (existing != null)
            {
                footprintVisualRoot = existing;
            }
            else
            {
                var visualObject = new GameObject("Rectangular Footprint Visual");
                footprintVisualRoot = visualObject.transform;
                footprintVisualRoot.SetParent(transform, false);
            }
        }

        footprintVisualRoot.localPosition = new Vector3(0f, 0f, GetFootprintWorldZ() - transform.position.z);
        footprintVisualRoot.localRotation = Quaternion.identity;
        footprintVisualRoot.localScale = Vector3.one;

        if (footprintMeshFilter == null)
            footprintMeshFilter = footprintVisualRoot.GetComponent<MeshFilter>();
        if (footprintMeshRenderer == null)
            footprintMeshRenderer = footprintVisualRoot.GetComponent<MeshRenderer>();

        if (footprintMeshFilter == null)
            footprintMeshFilter = footprintVisualRoot.gameObject.AddComponent<MeshFilter>();
        if (footprintMeshRenderer == null)
            footprintMeshRenderer = footprintVisualRoot.gameObject.AddComponent<MeshRenderer>();
        if (footprintBorderRenderer == null)
            footprintBorderRenderer = footprintVisualRoot.GetComponent<LineRenderer>();
        if (footprintBorderRenderer == null)
            footprintBorderRenderer = footprintVisualRoot.gameObject.AddComponent<LineRenderer>();

        if (footprintMeshFilter.sharedMesh == null)
        {
            var mesh = new Mesh { name = "Rectangular Footprint" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            footprintMeshFilter.sharedMesh = mesh;
        }

        if (footprintMeshRenderer.sharedMaterial == null)
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            material.color = footprintVisualColor;
            footprintMeshRenderer.sharedMaterial = material;
        }

        ConfigureBorderRenderer();
    }

    private void SetFootprintVisualVisible(bool visible)
    {
        if (footprintVisualRoot != null)
            footprintVisualRoot.gameObject.SetActive(visible);
        if (footprintMeshRenderer != null)
            footprintMeshRenderer.enabled = visible;
        if (footprintBorderRenderer != null)
            footprintBorderRenderer.enabled = visible;
    }

    private void ConfigureBorderRenderer()
    {
        if (footprintBorderRenderer == null)
            return;

        footprintBorderRenderer.useWorldSpace = false;
        footprintBorderRenderer.loop = true;
        footprintBorderRenderer.positionCount = 4;
        footprintBorderRenderer.SetPositions(new[]
        {
            new Vector3(-0.5f, -0.5f, -0.01f),
            new Vector3(-0.5f, 0.5f, -0.01f),
            new Vector3(0.5f, 0.5f, -0.01f),
            new Vector3(0.5f, -0.5f, -0.01f),
        });
        if (footprintBorderRenderer.sharedMaterial == null)
            footprintBorderRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        footprintBorderRenderer.startColor = footprintBorderColor;
        footprintBorderRenderer.endColor = footprintBorderColor;
        footprintBorderRenderer.startWidth = footprintBorderWidth;
        footprintBorderRenderer.endWidth = footprintBorderWidth;
        footprintBorderRenderer.sortingOrder = footprintBorderSortingOrder;
    }

    public bool TryGetWorldCollisionBounds(out Bounds bounds)
    {
        Vector2 fieldToWorld = GetFieldToWorldScale();
        Vector2 paddedPhysicalSize = new Vector2(
            physicalSizeMeters.x * Mathf.Max(0.001f, collisionSizeMultiplier.x) + collisionPaddingMeters.x * 2f,
            physicalSizeMeters.y * Mathf.Max(0.001f, collisionSizeMultiplier.y) + collisionPaddingMeters.y * 2f);

        bounds = new Bounds(
            new Vector3(transform.position.x, transform.position.y, GetFootprintWorldZ()),
            new Vector3(
                Mathf.Max(0.001f, paddedPhysicalSize.x * fieldToWorld.x),
                Mathf.Max(0.001f, paddedPhysicalSize.y * fieldToWorld.y),
                0.001f));
        return true;
    }

    private float GetObjectCenterWorldZ()
    {
        if (!placeCenterAboveFieldSurface)
            return worldZ;

        return GetFieldSurfaceZ() + GetCameraDepthDirection() * GetObjectHeightWorld() * 0.5f;
    }

    private float GetFootprintWorldZ()
    {
        if (!placeCenterAboveFieldSurface)
            return worldZ;

        return GetFieldSurfaceZ() + GetCameraDepthDirection() * Mathf.Max(0f, footprintSurfaceLift);
    }

    private float GetFieldSurfaceZ()
    {
        if (TryGetFrameBounds(out var bounds))
            return bounds.center.z;

        return worldZ;
    }

    private float GetObjectHeightWorld()
    {
        Vector2 fieldToWorld = GetFieldToWorldScale();
        float averageScale = (Mathf.Abs(fieldToWorld.x) + Mathf.Abs(fieldToWorld.y)) * 0.5f;
        return Mathf.Max(0f, physicalSizeMeters.z * averageScale);
    }

    private float GetCameraDepthDirection()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return -1f;

        return camera.transform.position.z <= GetFieldSurfaceZ() ? -1f : 1f;
    }
}
