using System.Collections.Generic;
using System.Linq;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Logging;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4ManifestValidationTests
    {
        private V4PassManifest _manifest;

        [SetUp]
        public void SetUp()
        {
            _manifest = ScriptableObject.CreateInstance<V4PassManifest>();
            _manifest.manifestName = "test";
            _manifest.uavBudget = 8;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_manifest);
        }

        private static V4PassDef MakePass(string id, int rwCount, ComputeShader shader = null, string kernel = "Missing")
        {
            var rw = new string[rwCount];
            for (int i = 0; i < rwCount; i++)
            {
                rw[i] = $"_Rw{i}";
            }

            return new V4PassDef
            {
                passId = id,
                shader = shader,
                kernelName = kernel,
                readWriteBuffers = rw,
                readOnlyBuffers = new string[0]
            };
        }

        [Test]
        public void NullManifest_ReportsError()
        {
            List<V4ManifestValidation.Issue> issues = V4ManifestValidation.Validate(null);
            Assert.IsTrue(issues.Any(i => i.Severity == V4LogLevel.Error));
        }

        [Test]
        public void OverUavBudget_ReportsError()
        {
            _manifest.passes.Add(MakePass("overBudget", 9));
            List<V4ManifestValidation.Issue> issues = V4ManifestValidation.Validate(_manifest);
            Assert.IsTrue(issues.Any(i =>
                i.PassId == "overBudget" &&
                i.Severity == V4LogLevel.Error &&
                i.Message.Contains("UAV budget")));
        }

        [Test]
        public void ExactlyAtUavBudget_ReportsWarning()
        {
            _manifest.passes.Add(MakePass("atBudget", 8));
            List<V4ManifestValidation.Issue> issues = V4ManifestValidation.Validate(_manifest);
            Assert.IsTrue(issues.Any(i =>
                i.PassId == "atBudget" &&
                i.Severity == V4LogLevel.Warning &&
                i.Message.Contains("limit")));
        }

        [Test]
        public void UnderBudget_NoUavIssue()
        {
            _manifest.passes.Add(MakePass("underBudget", 4));
            List<V4ManifestValidation.Issue> issues = V4ManifestValidation.Validate(_manifest);
            Assert.IsFalse(issues.Any(i => i.PassId == "underBudget" && i.Message.Contains("UAV")));
        }

        [Test]
        public void MissingShader_ReportsError()
        {
            _manifest.passes.Add(MakePass("noShader", 1));
            List<V4ManifestValidation.Issue> issues = V4ManifestValidation.Validate(_manifest);
            Assert.IsTrue(issues.Any(i => i.PassId == "noShader" && i.Message.Contains("shader")));
        }

        [Test]
        public void DuplicatePassIds_ReportError()
        {
            _manifest.passes.Add(MakePass("dup", 1));
            _manifest.passes.Add(MakePass("dup", 1));
            List<V4ManifestValidation.Issue> issues = V4ManifestValidation.Validate(_manifest);
            Assert.IsTrue(issues.Any(i => i.Message.Contains("duplicate")));
        }

        [Test]
        public void DuplicateBufferInSamePass_ReportsError()
        {
            V4PassDef pass = MakePass("dupBuffer", 0);
            pass.readWriteBuffers = new[] { "_Same" };
            pass.readOnlyBuffers = new[] { "_Same" };
            _manifest.passes.Add(pass);

            List<V4ManifestValidation.Issue> issues = V4ManifestValidation.Validate(_manifest);
            Assert.IsTrue(issues.Any(i => i.PassId == "dupBuffer" && i.Message.Contains("more than once")));
        }

        [Test]
        public void IsUsable_FalseWhenAnyErrorExists()
        {
            _manifest.passes.Add(MakePass("overBudget", 9));
            Assert.IsFalse(V4ManifestValidation.IsUsable(_manifest, out _));
        }
    }
}
