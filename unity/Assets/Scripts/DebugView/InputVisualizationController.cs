using OomiyaFes.Field;
using OomiyaFes.Input;
using UnityEngine;

namespace OomiyaFes.DebugView
{
    public sealed class InputVisualizationController : MonoBehaviour
    {
        [SerializeField] private UdpTrackingReceiver receiver;
        [SerializeField] private Transform fieldPlane;
        [SerializeField] private Transform lightMarker;
        [SerializeField] private Transform hitMarker;
        [SerializeField] private LineRenderer directionLine;
        [SerializeField] private Renderer lightRenderer;
        [SerializeField] private Renderer hitRenderer;
        [SerializeField] private Color connectedColor = new Color(0.1f, 0.9f, 1.0f, 1.0f);
        [SerializeField] private Color disconnectedColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);
        [SerializeField] private float directionLength = 0.25f;

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
            var rotation = FieldCoordinateMapper.PythonEulerToUnity(message.field_euler_zyx_deg);
            var direction = rotation * Vector3.down;

            lightMarker.SetPositionAndRotation(position, rotation);
            UpdateDirectionLine(position, direction);

            if (FieldCoordinateMapper.TryProjectToField(position, direction, out var hit))
            {
                hitMarker.gameObject.SetActive(true);
                hitMarker.position = hit + Vector3.up * 0.003f;
            }
            else
            {
                hitMarker.gameObject.SetActive(false);
            }

            SetColor(receiver.IsConnected ? connectedColor : disconnectedColor);
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
        }

        private void UpdateDirectionLine(Vector3 position, Vector3 direction)
        {
            directionLine.SetPosition(0, position);
            directionLine.SetPosition(1, position + direction.normalized * directionLength);
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
        }
    }
}
