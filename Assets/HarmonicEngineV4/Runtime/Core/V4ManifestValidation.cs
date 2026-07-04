using System.Collections.Generic;
using HarmonicEngineV4.Logging;

namespace HarmonicEngineV4.Core
{
    /// <summary>
    /// Validates a pass manifest at authoring/init time (spec section 8.4) so UAV budget
    /// violations surface as warnings in the editor instead of cryptic runtime GPU errors.
    /// </summary>
    public static class V4ManifestValidation
    {
        public readonly struct Issue
        {
            public readonly V4LogLevel Severity;
            public readonly string PassId;
            public readonly string Message;

            public Issue(V4LogLevel severity, string passId, string message)
            {
                Severity = severity;
                PassId = passId;
                Message = message;
            }

            public override string ToString() => $"[{Severity}] pass '{PassId}': {Message}";
        }

        public static List<Issue> Validate(V4PassManifest manifest)
        {
            var issues = new List<Issue>();
            if (manifest == null)
            {
                issues.Add(new Issue(V4LogLevel.Error, "<manifest>", "manifest is null"));
                return issues;
            }

            var seenIds = new HashSet<string>();
            foreach (V4PassDef pass in manifest.passes)
            {
                string id = string.IsNullOrEmpty(pass.passId) ? "<unnamed>" : pass.passId;

                if (string.IsNullOrEmpty(pass.passId))
                {
                    issues.Add(new Issue(V4LogLevel.Error, id, "passId is empty"));
                }
                else if (!seenIds.Add(pass.passId))
                {
                    issues.Add(new Issue(V4LogLevel.Error, id, "duplicate passId"));
                }

                if (pass.shader == null)
                {
                    issues.Add(new Issue(V4LogLevel.Error, id, "shader reference is missing"));
                }
                else if (string.IsNullOrEmpty(pass.kernelName))
                {
                    issues.Add(new Issue(V4LogLevel.Error, id, "kernelName is empty"));
                }
                else if (!pass.shader.HasKernel(pass.kernelName))
                {
                    issues.Add(new Issue(V4LogLevel.Error, id, $"kernel '{pass.kernelName}' not found on shader '{pass.shader.name}'"));
                }

                int uavCount = pass.readWriteBuffers?.Length ?? 0;
                if (uavCount > manifest.uavBudget)
                {
                    issues.Add(new Issue(V4LogLevel.Error, id,
                        $"binds {uavCount} read-write buffers, exceeding the UAV budget of {manifest.uavBudget}"));
                }
                else if (uavCount == manifest.uavBudget)
                {
                    issues.Add(new Issue(V4LogLevel.Warning, id,
                        $"binds exactly {uavCount} read-write buffers - at the UAV budget limit, no headroom"));
                }

                var seenBuffers = new HashSet<string>();
                foreach (string buffer in pass.AllBuffers())
                {
                    if (string.IsNullOrEmpty(buffer))
                    {
                        issues.Add(new Issue(V4LogLevel.Error, id, "contains an empty buffer name"));
                    }
                    else if (!seenBuffers.Add(buffer))
                    {
                        issues.Add(new Issue(V4LogLevel.Error, id, $"buffer '{buffer}' is listed more than once"));
                    }
                }
            }

            return issues;
        }

        /// <summary>True when the manifest contains no Error-severity issues.</summary>
        public static bool IsUsable(V4PassManifest manifest, out List<Issue> issues)
        {
            issues = Validate(manifest);
            foreach (Issue issue in issues)
            {
                if (issue.Severity == V4LogLevel.Error)
                {
                    return false;
                }
            }

            return true;
        }

        public static void LogIssues(IEnumerable<Issue> issues)
        {
            foreach (Issue issue in issues)
            {
                V4Log.Write(issue.Severity, V4LogCategory.BufferBinding, issue.ToString());
            }
        }
    }
}
