using System;

namespace HarmonicEngineV4.Logging
{
    /// <summary>
    /// Per-run channel recording policy. Noisy channels (PassExecution, BufferBinding)
    /// default off; everything else records Info+ to disk.
    /// </summary>
    [Serializable]
    public sealed class V4ChannelLogSettings
    {
        public V4LogLevel minimumLevel = V4LogLevel.Info;

        public bool recordGeneral = true;
        public bool recordBake = true;
        public bool recordSpawn = true;

        /// <summary>Pass scopes log at Verbose; set minimumLevel to Verbose when enabling.</summary>
        public bool recordPassExecution = false;
        public bool recordZoneClassification = true;
        public bool recordCanvasSettle = true;
        public bool recordBufferBinding = false;
        public bool recordRecording = true;
        public bool recordBoundaryPressure = false;
        public bool recordWallEscapeForensics = true;
        public bool recordPerformance = true;

        public bool IsCategoryRecorded(V4LogCategory category)
        {
            switch (category)
            {
                case V4LogCategory.General: return recordGeneral;
                case V4LogCategory.Bake: return recordBake;
                case V4LogCategory.Spawn: return recordSpawn;
                case V4LogCategory.PassExecution: return recordPassExecution;
                case V4LogCategory.ZoneClassification: return recordZoneClassification;
                case V4LogCategory.CanvasSettle: return recordCanvasSettle;
                case V4LogCategory.BufferBinding: return recordBufferBinding;
                case V4LogCategory.Recording: return recordRecording;
                case V4LogCategory.BoundaryPressure: return recordBoundaryPressure;
                case V4LogCategory.WallEscapeForensics: return recordWallEscapeForensics;
                case V4LogCategory.Performance: return recordPerformance;
                default: return true;
            }
        }
    }
}
