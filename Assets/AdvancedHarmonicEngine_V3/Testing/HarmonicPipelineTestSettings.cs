using UnityEngine;

namespace HarmonicEngine.Testing
{
    [CreateAssetMenu(fileName = "HarmonicPipelineTestSettings", menuName = "HarmonicEngine/Test Pipeline Settings")]
    public class HarmonicPipelineTestSettings : ScriptableObject
    {
        public ComputeShader argumentUtilityShader;
        public ComputeShader spatialHashGridShader;
        public ComputeShader radixSortShader;
        public ComputeShader streamCompactionShader;
        public ComputeShader streamCompactionIntegrateShader;
        public ComputeShader pbfSolverShader;
        public ComputeShader dataCompactionShader;
        public ComputeShader fallingFluidWorldShader;
        public ComputeShader eulerianDragGridShader;
        public int testCapacity = 8192;
    }
}
