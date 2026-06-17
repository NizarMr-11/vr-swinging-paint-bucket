using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace HarmonicEngine.Infrastructure.PlaybackStreaming
{
    /// <summary>
    /// Background worker preloads quantized frame files into a sliding window queue.
    /// </summary>
    public sealed class SlidingWindowDiskQueue : IDisposable
    {
        private readonly ConcurrentQueue<string> _readyFrames = new();
        private readonly ConcurrentQueue<string> _loadRequests = new();
        private readonly Thread _worker;
        private volatile bool _running = true;

        public int WindowCapacity { get; }
        public int Count => _readyFrames.Count;

        public SlidingWindowDiskQueue(int windowCapacity = 8)
        {
            WindowCapacity = Math.Max(1, windowCapacity);
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "HarmonicSlidingWindowDiskQueue"
            };
            _worker.Start();
        }

        public void Enqueue(string framePath)
        {
            _loadRequests.Enqueue(framePath);
        }

        public bool TryDequeue(out string framePath)
        {
            return _readyFrames.TryDequeue(out framePath);
        }

        public bool TryPeek(out string framePath)
        {
            return _readyFrames.TryPeek(out framePath);
        }

        private void WorkerLoop()
        {
            while (_running)
            {
                if (_readyFrames.Count >= WindowCapacity)
                {
                    Thread.Sleep(1);
                    continue;
                }

                if (!_loadRequests.TryDequeue(out string path))
                {
                    Thread.Sleep(1);
                    continue;
                }

                if (!File.Exists(path))
                {
                    continue;
                }

                _readyFrames.Enqueue(path);
            }
        }

        public void Dispose()
        {
            _running = false;
            _worker.Join(2000);
        }
    }
}
