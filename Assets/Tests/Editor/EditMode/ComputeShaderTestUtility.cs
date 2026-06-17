using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests
{
    internal static class ComputeShaderTestUtility
    {
        public static bool HasKernel(ComputeShader shader, string kernelName)
        {
            if (shader == null)
            {
                return false;
            }

            LogAssert.ignoreFailingMessages = true;
            try
            {
                return shader.FindKernel(kernelName) >= 0;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        public static void AssertHasKernel(ComputeShader shader, string kernelName)
        {
            Assert.IsTrue(HasKernel(shader, kernelName), $"Kernel '{kernelName}' not found on {shader?.name}");
        }
    }
}
