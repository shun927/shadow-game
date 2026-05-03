using OomiyaFes.Field;
using OomiyaFes.Input;
using UnityEngine;

namespace OomiyaFes.DebugView
{
    public sealed class InputVisualizationController : MonoBehaviour
    {
        [SerializeField] private UdpTrackingReceiver receiver;
        [SerializeField] private Transform fieldPlane;
        [SerializeField] private Transform markerPlane;
        [SerializeField] private Transform lightMarker;
        [SerializeField] private Transform hitMarker;
        [SerializeField] private Light spotLight;
        [SerializeField] private LineRenderer directionLine;
        [SerializeField] private Renderer lightRenderer;
        [SerializeField] private Renderer hitRenderer;
        [SerializeField] private Color connectedColor = new Color(0.1f, 0.9f, 1.0f, 1.0f);
        [SerializeField] private Color disconnectedColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);
        [SerializeField] private float directionLength = 0.25f;
        [SerializeField] private float spotRange = 0.65f;
        [SerializeField] private float spotAngle = 28f;
        [SerializeField] private float spotIntensity = 6f;
        [SerializeField] private float movingMarkerSize = 0.07f;
        [SerializeField] private bool invertLightDirection;

        private Vector3 latestPosition;
        private Vector3 latestDirection = Vector3.down;
        private Vector3 latestNormal = Vector3.down;
        private Vector3 latestHit;
        private bool hasLatestPose;
        private bool hasLatestHit;

        private void Reset()
        {
            receiver = FindFirstObjectByType<UdpTrackingReceiver>();
        }

        private void Awake()
        {
            EnsureRuntimeObjects();
        }

        private void Update()
        {
            if (receiver == null || !receiver.HasPose)
            {
                SetColor(disconnectedColor);
                return;
            }

            var message = receiver.Latest;
            var position = FieldCoordinateMapper.PythonToUnity(message.field_xyz_m);
            var rotation = FieldCoordinateMapper.TryMarkerBasisToUnityRotation(message, out var basisRotation)
                ? basisRotation
                : FieldCoordinateMapper.PythonEulerToUnity(message.field_euler_zyx_deg);
            var normal = rotation * Vector3.forward;
            var direction = invertLightDirection ? -normal : normal;

            latestPosition = position;
            latestDirection = direction.normalized;
            latestNormal = normal.normalized;
            hasLatestPose = true;

            markerPlane.SetPositionAndRotation(position, rotation);
            lightMarker.SetPositionAndRotation(position, rotation);
            UpdateSpotLight(position, direction);
            UpdateDirectionLine(position, direction);

            if (FieldCoordinateMapper.TryProjectToField(position, direction, out var hit))
            {
                hitMarker.gameObject.SetActive(true);
                hitMarker.position = hit + Vector3.up * 0.003f;
                latestHit = hit;
                hasLatestHit = true;
            }
            else
            {
                hitMarker.gameObject.SetActive(false);
                hasLatestHit = false;
            }

            SetColor(receiver.IsConnected ? connectedColor : disconnectedColor);
        }

        private void OnDrawGizmos()
        {
            var half = FieldCoordinateMapper.FieldSizeMeters * 0.5f;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(FieldCoordinateMapper.FieldSizeMeters, 0.002f, FieldCoordinateMapper.FieldSizeMeters));

            Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
            Gizmos.DrawLine(new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f));
            Gizmos.DrawLine(new Vector3(0f, 0f, -half), new Vector3(0f, 0f, half));
            Gizmos.DrawLine(Vector3.zero, Vector3.up * 0.3f);

            if (!hasLatestPose)
            {
                return;
            }

            Gizmos.color = connectedColor;
            Gizmos.DrawWireSphere(latestPosition, 0.025f);
            Gizmos.DrawLine(latestPosition, latestPosition + latestNormal * 0.08f);
            Gizmos.DrawLine(latestPosition, latestPosition + latestDirection * directionLength);

            if (hasLatestHit)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
                Gizmos.DrawWireSphere(latestHit, 0.018f);
                Gizmos.DrawLine(latestPosition, latestHit);
            }
        }

        private void EnsureRuntimeObjects()
        {
            if (fieldPlane == null)
            {
                var field = GameObject.CreatePrimitive(PrimitiveType.Cube);
                field.name = "Field 30cm x 30cm";
                field.transform.SetParent(transform, false);
                field.transform.localScale = new Vector3(FieldCoordinateMapper.FieldSizeMeters, 0.005f, FieldCoordinateMapper.FieldSizeMeters);
                fieldPlane = field.transform;
            }

            if (markerPlane == null)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = "Tracked AR Marker Plane";
                marker.transform.SetParent(transform, false);
                markerPlane = marker.transform;
            }
            markerPlane.localScale = Vector3.one * movingMarkerSize;

            if (lightMarker == null)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Tracked Light";
                marker.transform.SetParent(transform, false);
                marker.transform.localScale = Vector3.one * 0.025f;
                lightMarker = marker.transform;
                lightRenderer = marker.GetComponent<Renderer>();
            }

            if (hitMarker == null)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Field Hit Point";
                marker.transform.SetParent(transform, false);
                marker.transform.localScale = Vector3.one * 0.018f;
                hitMarker = marker.transform;
                hitRenderer = marker.GetComponent<Renderer>();
            }

            if (directionLine == null)
            {
                var line = new GameObject("Light Direction");
                line.transform.SetParent(transform, false);
                directionLine = line.AddComponent<LineRenderer>();
                directionLine.positionCount = 2;
                directionLine.widthMultiplier = 0.006f;
                directionLine.useWorldSpace = true;
                directionLine.material = new Material(Shader.Find("Sprites/Default"));
            }

            if (spotLight == null)
            {
                var lightObject = new GameObject("Tracked Spot Light");
                lightObject.transform.SetParent(transform, false);
                spotLight = lightObject.AddComponent<Light>();
                spotLight.type = LightType.Spot;
                spotLight.shadows = LightShadows.Soft;
            }

            ApplySpotLightSettings();
        }

        private void UpdateDirectionLine(Vector3 position, Vector3 direction)
        {
            directionLine.SetPosition(0, position);
            directionLine.SetPosition(1, position + direction.normalized * directionLength);
        }

        private void UpdateSpotLight(Vector3 position, Vector3 direction)
        {
            if (spotLight == null)
            {
                return;
            }

            spotLight.transform.position = position;
            spotLight.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            ApplySpotLightSettings();
        }

        private void ApplySpotLightSettings()
        {
            if (spotLight == null)
            {
                return;
            }

            spotLight.range = spotRange;
            spotLight.spotAngle = spotAngle;
            spotLight.intensity = spotIntensity;
            spotLight.color = connectedColor;
        }

        private void SetColor(Color color)
        {
            if (lightRenderer != null)
            {
                lightRenderer.material.color = color;
            }
            if (hitRenderer != null)
            {
                hitRenderer.material.color = color;
            }
            if (directionLine != null)
            {
                directionLine.startColor = color;
                directionLine.endColor = color;
            }
            if (spotLight != null)
            {
                spotLight.enabled = color.a > 0.5f;
            }
        }
    }
}
