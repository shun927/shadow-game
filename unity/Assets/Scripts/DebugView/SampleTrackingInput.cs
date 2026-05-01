using OomiyaFes.Field;
using UnityEngine;

namespace OomiyaFes.DebugView
{
    public sealed class SampleTrackingInput : MonoBehaviour
    {
        [SerializeField] private bool enableSampleMotion;
        [SerializeField] private Transform target;
        [SerializeField] private float radius = 0.08f;
        [SerializeField] private float height = 0.12f;

        private void Update()
        {
            if (!enableSampleMotion || target == null)
            {
                return;
            }

            var t = Time.time;
            target.position = new Vector3(
                Mathf.Cos(t) * radius,
                height,
                Mathf.Sin(t) * radius
            );
            target.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        }
    }
}
