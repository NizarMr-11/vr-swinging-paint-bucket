using UnityEngine;

namespace HarmonicEngineV4.Logging
{
    /// <summary>Routes V4Log output to the Unity console.</summary>
    public sealed class V4ConsoleLogSink : IV4LogSink
    {
        public void Write(V4LogLevel level, V4LogCategory category, string message)
        {
            string line = $"[V4:{category}] {message}";
            switch (level)
            {
                case V4LogLevel.Warning:
                    Debug.LogWarning(line);
                    break;
                case V4LogLevel.Error:
                    Debug.LogError(line);
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
        }

        public void Flush()
        {
        }
    }
}
