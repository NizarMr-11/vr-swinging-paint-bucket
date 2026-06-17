using Unity.Mathematics;

namespace HarmonicEngine.Core.Mathematics.Integrators
{
    /// <summary>
    /// Classic RK4 integrator for auxiliary CPU systems (pendulum kinematics, etc.).
    /// Burst-friendly: pure static methods over blittable types.
    /// </summary>
    public static class Rk4SystemSolver
    {
        public delegate float3 Derivative(float3 state, float time);

        public static float3 Integrate(float3 initialState, float time, float deltaTime, Derivative derivative)
        {
            float3 k1 = derivative(initialState, time);
            float3 k2 = derivative(initialState + k1 * (deltaTime * 0.5f), time + deltaTime * 0.5f);
            float3 k3 = derivative(initialState + k2 * (deltaTime * 0.5f), time + deltaTime * 0.5f);
            float3 k4 = derivative(initialState + k3 * deltaTime, time + deltaTime);
            return initialState + (deltaTime / 6f) * (k1 + 2f * k2 + 2f * k3 + k4);
        }

        public static float IntegrateScalar(float initialState, float time, float deltaTime, System.Func<float, float, float> derivative)
        {
            float k1 = derivative(initialState, time);
            float k2 = derivative(initialState + k1 * (deltaTime * 0.5f), time + deltaTime * 0.5f);
            float k3 = derivative(initialState + k2 * (deltaTime * 0.5f), time + deltaTime * 0.5f);
            float k4 = derivative(initialState + k3 * deltaTime, time + deltaTime);
            return initialState + (deltaTime / 6f) * (k1 + 2f * k2 + 2f * k3 + k4);
        }
    }
}
