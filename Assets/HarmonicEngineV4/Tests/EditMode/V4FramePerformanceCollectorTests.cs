using System.Collections.Generic;
using HarmonicEngineV4.Logging;
using NUnit.Framework;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4FramePerformanceCollectorTests
    {
        [SetUp]
        public void SetUp()
        {
            V4FramePerformanceCollector.Enabled = true;
            V4FramePerformanceCollector.GpuSyncEnabled = false;
            V4FramePerformanceCollector.ProfilerMarkersEnabled = false;
            V4FramePerformanceCollector.BeginFrame(10, 500);
            V4FramePerformanceCollector.SetDebugFlags("boundaryPressure");
        }

        [TearDown]
        public void TearDown()
        {
            V4FramePerformanceCollector.Enabled = false;
            V4FramePerformanceCollector.GpuSyncEnabled = false;
            V4FramePerformanceCollector.ProfilerMarkersEnabled = false;
        }

        [Test]
        public void RecordCpuScope_PassScopesDoNotContributeToTotal()
        {
            V4FramePerformanceCollector.RecordCpuScope("Density", 2.5d);
            V4FramePerformanceCollector.RecordCpuScope("Phase_PbfLoop", 4.0d, contributesToTotal: true);

            string ranked = V4FramePerformanceCollector.FormatTopScopes(
                new Dictionary<string, double> { { "Density", 2.5d }, { "Phase_PbfLoop", 4.0d } },
                "topCpu");

            StringAssert.Contains("Phase_PbfLoop:4.00", ranked);
            StringAssert.Contains("Density:2.50", ranked);
        }

        [Test]
        public void FormatTopScopes_OrdersByDurationDescending()
        {
            var scopes = new Dictionary<string, double>
            {
                { "A", 1.0d },
                { "B", 3.0d },
                { "C", 2.0d }
            };

            string ranked = V4FramePerformanceCollector.FormatTopScopes(scopes, "topCpu");
            Assert.AreEqual("topCpu=B:3.00,C:2.00,A:1.00", ranked);
        }

        [Test]
        public void GpuSyncScope_RequiresGpuSyncEnabled()
        {
            V4FramePerformanceCollector.GpuSyncEnabled = false;
            V4FramePerformanceCollector.RecordGpuSyncScope("Density", 1.5d);

            string ranked = V4FramePerformanceCollector.FormatTopScopes(
                new Dictionary<string, double>(),
                "topGpuSync");
            Assert.AreEqual("topGpuSync=", ranked);
        }

        [Test]
        public void GpuSyncScope_RecordsWhenEnabled()
        {
            V4FramePerformanceCollector.GpuSyncEnabled = true;
            V4FramePerformanceCollector.RecordGpuSyncScope("Density", 1.5d);
            V4FramePerformanceCollector.RecordGpuSyncScope("Finalize", 0.8d);

            var scopes = new Dictionary<string, double>
            {
                { "Density", 1.5d },
                { "Finalize", 0.8d }
            };
            string ranked = V4FramePerformanceCollector.FormatTopScopes(scopes, "topGpuSync");
            StringAssert.Contains("Density:1.50", ranked);
            StringAssert.Contains("Finalize:0.80", ranked);
        }
    }
}
