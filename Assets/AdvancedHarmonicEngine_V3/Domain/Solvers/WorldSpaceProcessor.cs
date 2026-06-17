using Unity.Mathematics;

namespace HarmonicEngine.Domain.Solvers
{
    public static class WorldSpaceProcessor
    {
        public static float3 IntegrateVelocity(float3 velocity, float3 gravity, float dt)
        {
            return velocity + gravity * dt;
        }

        public static float3 IntegratePosition(float3 position, float3 velocity, float dt)
        {
            return position + velocity * dt;
        }

        public static float3 ApplyDrag(float3 velocity, float dragCoefficient, float dt)
        {
            float decay = math.max(0f, 1f - dragCoefficient * dt);
            return velocity * decay;
        }
    }
}
