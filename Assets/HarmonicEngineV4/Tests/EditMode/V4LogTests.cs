using System.Collections.Generic;
using HarmonicEngineV4.Logging;
using NUnit.Framework;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4LogTests
    {
        private sealed class CaptureSink : IV4LogSink
        {
            public readonly List<(V4LogLevel level, V4LogCategory category, string message)> Lines =
                new List<(V4LogLevel, V4LogCategory, string)>();

            public void Write(V4LogLevel level, V4LogCategory category, string message)
            {
                Lines.Add((level, category, message));
            }

            public void Flush()
            {
            }
        }

        private CaptureSink _sink;

        [SetUp]
        public void SetUp()
        {
            V4Log.ClearSinks();
            V4Log.MinimumLevel = V4LogLevel.Verbose;
            V4Log.SetCategoryEnabled(V4LogCategory.PassExecution, true);
            _sink = new CaptureSink();
            V4Log.AddSink(_sink);
        }

        [TearDown]
        public void TearDown()
        {
            V4Log.ClearSinks();
            V4Log.MinimumLevel = V4LogLevel.Info;
            foreach (V4LogCategory category in System.Enum.GetValues(typeof(V4LogCategory)))
            {
                V4Log.SetCategoryEnabled(category, true);
            }
        }

        [Test]
        public void Write_RoutesToSink()
        {
            V4Log.Info(V4LogCategory.Bake, "hello");
            Assert.AreEqual(1, _sink.Lines.Count);
            Assert.AreEqual(V4LogLevel.Info, _sink.Lines[0].level);
            Assert.AreEqual(V4LogCategory.Bake, _sink.Lines[0].category);
        }

        [Test]
        public void MinimumLevel_FiltersLowerLevels()
        {
            V4Log.MinimumLevel = V4LogLevel.Warning;
            V4Log.Info(V4LogCategory.Bake, "dropped");
            V4Log.Warning(V4LogCategory.Bake, "kept");
            Assert.AreEqual(1, _sink.Lines.Count);
            Assert.AreEqual("kept", _sink.Lines[0].message);
        }

        [Test]
        public void DisabledCategory_IsSilenced()
        {
            V4Log.SetCategoryEnabled(V4LogCategory.PassExecution, false);
            V4Log.Info(V4LogCategory.PassExecution, "dropped");
            V4Log.Info(V4LogCategory.Bake, "kept");
            Assert.AreEqual(1, _sink.Lines.Count);
            Assert.AreEqual(V4LogCategory.Bake, _sink.Lines[0].category);
        }

        [Test]
        public void PassScope_LogsDurationOnDispose()
        {
            using (V4Log.PassScope scope = V4Log.BeginPass("TestPass"))
            {
                scope.RecordBufferBytes(1024);
            }

            Assert.AreEqual(1, _sink.Lines.Count);
            StringAssert.Contains("pass=TestPass", _sink.Lines[0].message);
            StringAssert.Contains("bufferBytes=1024", _sink.Lines[0].message);
        }

        [Test]
        public void PerfPhaseScope_EmitsPerformanceSummary()
        {
            V4FramePerformanceCollector.Enabled = true;
            V4FramePerformanceCollector.BeginFrame(1, 100);
            using (V4Log.BeginPerfPhase("PrePbf"))
            {
            }

            V4FramePerformanceCollector.EndFrame();

            Assert.IsTrue(_sink.Lines.Exists(line =>
                line.category == V4LogCategory.Performance &&
                line.message.Contains("Phase_PrePbf")));
            V4FramePerformanceCollector.Enabled = false;
        }

        [Test]
        public void PassScope_DisposeTwice_LogsOnce()
        {
            V4Log.PassScope scope = V4Log.BeginPass("Once");
            scope.Dispose();
            scope.Dispose();
            Assert.AreEqual(1, _sink.Lines.Count);
        }
    }
}
