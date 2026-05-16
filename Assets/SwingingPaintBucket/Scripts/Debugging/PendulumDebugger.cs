

using UnityEngine;
using SwingingPaintBucket.Pendulum;

namespace SwingingPaintBucket.Debugging
{
    public class PendulumDebugger : MonoBehaviour
    {
        private PendulumSimulator _pendulum;

        [Header("إعدادات العرض")]
        public bool ShowGizmos = true;
        public Color RopeColor = Color.white;

        private void Start()
        {
            _pendulum = GetComponent<PendulumSimulator>();

            if (_pendulum == null)
            {
                Debug.LogError("[PendulumDebugger] لا يوجد PendulumSimulator على هذا الـ GameObject!");
                return;
            }

            // حساب الدورة النظرية: T = 2π√(L/g)
            float theoreticalPeriod = 2f * Mathf.PI *
                Mathf.Sqrt(_pendulum.RopeLength / _pendulum.Gravity);

            Debug.Log($"[PendulumDebugger] الدورة النظرية: {theoreticalPeriod:F3} ثانية");
            Debug.Log($"[PendulumDebugger] طول الحبل: {_pendulum.RopeLength} م");
            Debug.Log($"[PendulumDebugger] الجاذبية: {_pendulum.Gravity} م/ث²");
        }

        private void Update()
        {
            if (_pendulum == null) return;

            // عرض القيم الحية في كل frame
            Debug.Log($"θ = {(_pendulum.Theta * Mathf.Rad2Deg):F2}°  |  " +
                      $"ω = {_pendulum.Omega:F3} rad/s  |  " +
                      $"pos = {transform.position}");
        }

        private void OnDrawGizmos()
        {
            if (!ShowGizmos || _pendulum == null) return;

            // رسم الحبل كخط في Scene View
            Gizmos.color = RopeColor;
            Gizmos.DrawLine(_pendulum.PivotPoint, transform.position);

            // رسم نقطة التعليق
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_pendulum.PivotPoint, 0.1f);
        }
    }
}
