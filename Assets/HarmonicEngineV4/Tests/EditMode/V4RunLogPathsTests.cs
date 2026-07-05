using System.IO;
using HarmonicEngineV4.Logging;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4RunLogPathsTests
    {
        [Test]
        public void DefaultRunsDirectory_IsUnderProjectLogsEngine2()
        {
            string path = V4RunLogPaths.DefaultRunsDirectory();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            Assert.IsTrue(path.StartsWith(projectRoot));
            StringAssert.Contains("Logs", path);
            StringAssert.Contains("Engine2", path);
        }
    }
}
