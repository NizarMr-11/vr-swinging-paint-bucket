using HarmonicEngineV4.Logging;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Display labels and mapping for run log channels in the setup wizard.</summary>
    public static class V4Lab2LoggingChannels
    {
        public static readonly V4LogCategory[] All =
        {
            V4LogCategory.General,
            V4LogCategory.Bake,
            V4LogCategory.Spawn,
            V4LogCategory.PassExecution,
            V4LogCategory.ZoneClassification,
            V4LogCategory.CanvasSettle,
            V4LogCategory.BufferBinding,
            V4LogCategory.Recording,
            V4LogCategory.BoundaryPressure,
            V4LogCategory.WallEscapeForensics,
            V4LogCategory.Performance
        };

        public static string GetLabel(V4LogCategory category)
        {
            switch (category)
            {
                case V4LogCategory.General: return "General";
                case V4LogCategory.Bake: return "Bake";
                case V4LogCategory.Spawn: return "Spawn";
                case V4LogCategory.PassExecution: return "Pass execution (verbose)";
                case V4LogCategory.ZoneClassification: return "Zone classification";
                case V4LogCategory.CanvasSettle: return "Canvas settle";
                case V4LogCategory.BufferBinding: return "Buffer binding";
                case V4LogCategory.Recording: return "Recording";
                case V4LogCategory.BoundaryPressure: return "Boundary pressure";
                case V4LogCategory.WallEscapeForensics: return "Wall escape forensics";
                case V4LogCategory.Performance: return "Performance";
                default: return category.ToString();
            }
        }

        public static bool IsRecorded(V4ChannelLogSettings settings, V4LogCategory category)
        {
            return settings != null && settings.IsCategoryRecorded(category);
        }

        public static void SetRecorded(V4ChannelLogSettings settings, V4LogCategory category, bool value)
        {
            if (settings == null)
            {
                return;
            }

            switch (category)
            {
                case V4LogCategory.General: settings.recordGeneral = value; break;
                case V4LogCategory.Bake: settings.recordBake = value; break;
                case V4LogCategory.Spawn: settings.recordSpawn = value; break;
                case V4LogCategory.PassExecution: settings.recordPassExecution = value; break;
                case V4LogCategory.ZoneClassification: settings.recordZoneClassification = value; break;
                case V4LogCategory.CanvasSettle: settings.recordCanvasSettle = value; break;
                case V4LogCategory.BufferBinding: settings.recordBufferBinding = value; break;
                case V4LogCategory.Recording: settings.recordRecording = value; break;
                case V4LogCategory.BoundaryPressure: settings.recordBoundaryPressure = value; break;
                case V4LogCategory.WallEscapeForensics: settings.recordWallEscapeForensics = value; break;
                case V4LogCategory.Performance: settings.recordPerformance = value; break;
            }
        }

        public static V4ChannelLogSettings CloneSettings(V4ChannelLogSettings source)
        {
            if (source == null)
            {
                return new V4ChannelLogSettings();
            }

            return UnityEngine.JsonUtility.FromJson<V4ChannelLogSettings>(
                UnityEngine.JsonUtility.ToJson(source));
        }
    }
}
