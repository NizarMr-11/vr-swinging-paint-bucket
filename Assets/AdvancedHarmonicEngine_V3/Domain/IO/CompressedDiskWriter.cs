using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;

namespace HarmonicEngine.Domain.IO
{
    public sealed class CompressedDiskWriter : IDisposable
    {
        private readonly ConcurrentQueue<WriteJob> _queue = new();
        private readonly Thread _worker;
        private volatile bool _running = true;

        public CompressedDiskWriter()
        {
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "HarmonicCompressedDiskWriter"
            };
            _worker.Start();
        }

        public void WriteFrame(byte[] payload, string filePath)
        {
            if (payload == null || payload.Length == 0)
            {
                return;
            }

            _queue.Enqueue(new WriteJob
            {
                Payload = payload,
                FilePath = filePath
            });
        }

        public void WriteFrameSync(byte[] payload, string filePath)
        {
            if (payload == null || payload.Length == 0)
            {
                Debug.LogWarning("[CompressedDiskWriter] Empty payload skipped.");
                return;
            }

            WritePayload(payload, filePath);
        }

        public int PendingWriteCount => _queue.Count;

        private void WorkerLoop()
        {
            while (_running)
            {
                if (_queue.TryDequeue(out WriteJob job))
                {
                    try
                    {
                        WritePayload(job.Payload, job.FilePath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[CompressedDiskWriter] Failed writing {job.FilePath}: {ex.Message}");
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }

            while (_queue.TryDequeue(out WriteJob job))
            {
                WritePayload(job.Payload, job.FilePath);
            }
        }

        private static void WritePayload(byte[] payload, string filePath)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(filePath, payload);
        }

        public void Dispose()
        {
            _running = false;
            _worker.Join(2000);
        }

        private struct WriteJob
        {
            public byte[] Payload;
            public string FilePath;
        }
    }
}
