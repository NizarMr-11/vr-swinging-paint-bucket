using UnityEngine;

namespace HarmonicEngineV4.Core
{
    /// <summary>
    /// Pure CPU reference for the expected-loss EMA (spec section 5, Level 2 approximation):
    ///   expectedLoss[hole] = lerp(expectedLoss[hole], actualEjectedThisFrame[hole], smoothing)
    /// The GPU mirror is the EmaUpdateKernel in V4Classification.compute.
    /// </summary>
    public sealed class V4EmaLossModel
    {
        private readonly float[] _expectedLoss;

        public float Smoothing { get; }
        public int HoleCount { get; }

        public V4EmaLossModel(int holeCount, float smoothing)
        {
            HoleCount = Mathf.Max(0, holeCount);
            Smoothing = Mathf.Clamp01(smoothing);
            _expectedLoss = new float[HoleCount];
        }

        public float ExpectedLoss(int hole) => _expectedLoss[hole];

        public float TotalExpectedLoss
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _expectedLoss.Length; i++)
                {
                    total += _expectedLoss[i];
                }

                return total;
            }
        }

        public void Update(int[] actualEjectedThisFrame)
        {
            for (int i = 0; i < HoleCount; i++)
            {
                int actual = i < actualEjectedThisFrame.Length ? actualEjectedThisFrame[i] : 0;
                _expectedLoss[i] = Mathf.Lerp(_expectedLoss[i], actual, Smoothing);
            }
        }

        /// <summary>Single-hole reference step used by GPU parity tests.</summary>
        public static float Step(float previous, int actualEjected, float smoothing)
        {
            return Mathf.Lerp(previous, actualEjected, Mathf.Clamp01(smoothing));
        }
    }
}
