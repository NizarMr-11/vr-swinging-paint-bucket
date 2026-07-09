using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>Scene marker for the pendulum hang point. Assign to V4SphericalPendulumController.pivotTransform.</summary>
    [DisallowMultipleComponent]
    public sealed class V4PendulumPivot : MonoBehaviour
    {
        [Min(0.01f)] public float gizmoRadius = 0.08f;
        public Color gizmoColor = new Color(1f, 0.85f, 0.2f, 0.9f);

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, gizmoRadius * 0.75f);
        }
    }
}
