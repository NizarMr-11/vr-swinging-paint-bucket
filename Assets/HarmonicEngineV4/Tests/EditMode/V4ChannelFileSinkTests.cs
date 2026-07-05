using System.IO;
using HarmonicEngineV4.Logging;
using NUnit.Framework;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4ChannelFileSinkTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "V4ChannelSinkTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Test]
        public void FileNameFor_EachCategory_IsStable()
        {
            Assert.AreEqual("bake.log", V4ChannelFileSink.FileNameFor(V4LogCategory.Bake));
            Assert.AreEqual("canvas_settle.log", V4ChannelFileSink.FileNameFor(V4LogCategory.CanvasSettle));
            Assert.AreEqual("pass_execution.log", V4ChannelFileSink.FileNameFor(V4LogCategory.PassExecution));
        }

        [Test]
        public void Write_RespectsCategoryToggleAndMinimumLevel()
        {
            var settings = new V4ChannelLogSettings
            {
                recordBake = true,
                recordPassExecution = false,
                minimumLevel = V4LogLevel.Warning
            };

            using (var sink = new V4ChannelFileSink(_tempDir, settings))
            {
                sink.Write(V4LogLevel.Info, V4LogCategory.Bake, "dropped-info");
                sink.Write(V4LogLevel.Warning, V4LogCategory.Bake, "kept-warning");
                sink.Write(V4LogLevel.Info, V4LogCategory.PassExecution, "dropped-channel");
                sink.Flush();
            }

            string bakePath = Path.Combine(_tempDir, "channels", V4ChannelFileSink.FileNameFor(V4LogCategory.Bake));
            Assert.IsTrue(File.Exists(bakePath));
            string bakeText = File.ReadAllText(bakePath);
            Assert.IsFalse(bakeText.Contains("dropped-info"));
            Assert.IsTrue(bakeText.Contains("kept-warning"));
            Assert.IsFalse(File.Exists(Path.Combine(_tempDir, "channels", V4ChannelFileSink.FileNameFor(V4LogCategory.PassExecution))));
        }
    }
}
