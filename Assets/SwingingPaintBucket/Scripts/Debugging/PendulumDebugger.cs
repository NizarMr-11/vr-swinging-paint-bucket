    using UnityEngine;
using SwingingPaintBucket.Pendulum;

namespace SwingingPaintBucket.Debugging
{
    public class PendulumDebugger : MonoBehaviour
    {
        private PendulumSimulator _pendulum;

        [Header("Display Settings")]
        public bool ShowGizmos = true;
        public Color RopeColor = Color.white;

        private void Start()
        {
            _pendulum = GetComponent<PendulumSimulator>();

            if (_pendulum == null)
            {
                Debug.LogError("[PendulumDebugger] No PendulumSimulator found on this GameObject!");
                return;
            }

            // Calculate theoretical period: T = 2π√(L/g)
            float theoreticalPeriod = 2f * Mathf.PI *
                Mathf.Sqrt(_pendulum.RopeLength / _pendulum.Gravity);

            Debug.Log($"[PendulumDebugger] Theoretical period: {theoreticalPeriod:F3} seconds");
            Debug.Log($"[PendulumDebugger] Rope length: {_pendulum.RopeLength} m");
            Debug.Log($"[PendulumDebugger] Gravity: {_pendulum.Gravity} m/s²");
        }

        private void Update()
        {
            if (_pendulum == null) return;

            // Display live values every frame
            Debug.Log($"θ = {(_pendulum.Theta * Mathf.Rad2Deg):F2}°  |  " +
                      $"ω = {_pendulum.Omega:F3} rad/s  |  " +
                      $"pos = {transform.position}");
        }

        private void OnDrawGizmos()
        {
            if (!ShowGizmos || _pendulum == null) return;

            // Draw rope as a line in Scene View
            Gizmos.color = RopeColor;
            Gizmos.DrawLine(_pendulum.PivotPoint, transform.position);

            // Draw pivot point
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_pendulum.PivotPoint, 0.1f);
        }
    }
}
