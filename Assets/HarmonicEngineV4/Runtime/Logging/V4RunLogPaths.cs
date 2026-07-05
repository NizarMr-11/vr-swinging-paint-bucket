using System.IO;
using UnityEngine;

namespace HarmonicEngineV4.Logging
{
    /// <summary>
    /// Default on-disk layout for V4 run logs: {projectRoot}/Logs/Engine2/run_*/...
    /// </summary>
    public static class V4RunLogPaths
    {
        public const string RelativeBase = "Logs/Engine2";

        /// <summary>Absolute path to Logs/Engine2 under the Unity project root.</summary>
        public static string DefaultRunsDirectory()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, RelativeBase.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
