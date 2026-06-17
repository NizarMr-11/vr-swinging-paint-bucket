using HarmonicEngine.Domain.IO;
using HarmonicEngine.Infrastructure.PlaybackStreaming;
using NUnit.Framework;
using System.IO;
using System.Threading;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class IoAndQueueTests
    {
        [Test]
        public void CompressedDiskWriter_WriteFrameSync_WritesBytesToDisk()
        {
            string path = Path.Combine(Application.temporaryCachePath, "harmonic_test_frame.bin");
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using var writer = new CompressedDiskWriter();
            byte[] payload = { 1, 2, 3, 4 };
            writer.WriteFrameSync(payload, path);

            Assert.IsTrue(File.Exists(path));
            CollectionAssert.AreEqual(payload, File.ReadAllBytes(path));
            File.Delete(path);
        }

        [Test]
        public void CompressedDiskWriter_AsyncWrite_CompletesOnWorkerThread()
        {
            string path = Path.Combine(Application.temporaryCachePath, "harmonic_async_frame.bin");
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using var writer = new CompressedDiskWriter();
            byte[] payload = { 9, 8, 7 };
            writer.WriteFrame(payload, path);

            for (int i = 0; i < 100 && !File.Exists(path); i++)
            {
                Thread.Sleep(10);
            }

            Assert.IsTrue(File.Exists(path));
            CollectionAssert.AreEqual(payload, File.ReadAllBytes(path));
            File.Delete(path);
        }

        [Test]
        public void CompressedDiskWriter_SkipsEmptyPayload()
        {
            using var writer = new CompressedDiskWriter();
            Assert.DoesNotThrow(() => writer.WriteFrame(null, "ignored.bin"));
            Assert.DoesNotThrow(() => writer.WriteFrame(new byte[0], "ignored.bin"));
        }

        [Test]
        public void SlidingWindowDiskQueue_EnqueueDequeueFifo()
        {
            string pathA = Path.Combine(Application.temporaryCachePath, "harmonic_queue_a.bin");
            string pathB = Path.Combine(Application.temporaryCachePath, "harmonic_queue_b.bin");
            File.WriteAllText(pathA, "a");
            File.WriteAllText(pathB, "b");

            using var queue = new SlidingWindowDiskQueue(8);
            queue.Enqueue(pathA);
            queue.Enqueue(pathB);

            for (int i = 0; i < 50 && queue.Count < 1; i++)
            {
                Thread.Sleep(10);
            }

            Assert.GreaterOrEqual(queue.Count, 1);
            Assert.IsTrue(queue.TryDequeue(out string first));
            Assert.AreEqual(pathA, first);

            File.Delete(pathA);
            File.Delete(pathB);
        }
    }
}
