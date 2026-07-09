using System.Diagnostics;
using System.IO;
using HarmonicEngineV4.Logging;
using UnityEngine;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Opens a native folder picker on Windows (editor or standalone).</summary>
    public static class V4Lab2FolderBrowser
    {
        public static string PickFolder(string title, string defaultPath)
        {
            string startPath = ResolveStartPath(defaultPath);
#if UNITY_EDITOR
            string picked = UnityEditor.EditorUtility.OpenFolderPanel(title, startPath, string.Empty);
            return string.IsNullOrEmpty(picked) ? null : picked;
#elif UNITY_STANDALONE_WIN
            return PickFolderWindows(title, startPath);
#else
            UnityEngine.Debug.LogWarning("Folder browse is only available on Windows. Enter a path manually.");
            return null;
#endif
        }

        public static string DefaultRunsDirectory()
        {
            return V4RunLogPaths.DefaultRunsDirectory();
        }

        private static string ResolveStartPath(string defaultPath)
        {
            if (!string.IsNullOrWhiteSpace(defaultPath) && Directory.Exists(defaultPath))
            {
                return defaultPath;
            }

            string fallback = DefaultRunsDirectory();
            if (Directory.Exists(fallback))
            {
                return fallback;
            }

            return System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static string PickFolderWindows(string title, string startPath)
        {
            string escapedTitle = EscapeForPowerShell(title);
            string escapedPath = EscapeForPowerShell(startPath);
            string script =
                "Add-Type -AssemblyName System.Windows.Forms; " +
                "$dialog = New-Object System.Windows.Forms.FolderBrowserDialog; " +
                $"$dialog.Description = '{escapedTitle}'; " +
                $"if (Test-Path '{escapedPath}') {{ $dialog.SelectedPath = '{escapedPath}'; }} " +
                "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { $dialog.SelectedPath }";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -STA -Command \"" + script + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using Process process = Process.Start(startInfo);
                if (process == null)
                {
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return string.IsNullOrEmpty(output) ? null : output;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Folder picker failed: {ex.Message}");
                return null;
            }
        }

        private static string EscapeForPowerShell(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("'", "''");
        }
#endif
    }
}
