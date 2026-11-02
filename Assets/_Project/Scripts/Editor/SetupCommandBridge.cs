using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Lets tools outside Unity (e.g. Claude Code) run the "Freelancer/Setup/..." menu items.
    /// Drop a menu path into Temp/freelancer-setup-command.txt; the result (OK or the errors
    /// logged while it ran) appears in Temp/freelancer-setup-result.txt.
    /// Only menu items under "Freelancer/Setup/" are allowed. Safe to delete this file.
    /// </summary>
    [InitializeOnLoad]
    static class SetupCommandBridge
    {
        const string CommandFile = "Temp/freelancer-setup-command.txt";
        const string ResultFile = "Temp/freelancer-setup-result.txt";
        const string AllowedPrefix = "Freelancer/Setup/";

        static double s_NextCheck;

        static SetupCommandBridge()
        {
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < s_NextCheck) return;
            s_NextCheck = EditorApplication.timeSinceStartup + 0.5;

            if (!File.Exists(CommandFile)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            string menuPath = File.ReadAllText(CommandFile).Trim();
            File.Delete(CommandFile);

            var log = new StringBuilder();
            if (!menuPath.StartsWith(AllowedPrefix, StringComparison.Ordinal))
            {
                File.WriteAllText(ResultFile, $"REFUSED: only {AllowedPrefix}* menu items may be run.\n");
                return;
            }

            void OnLog(string msg, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    log.AppendLine($"{type}: {msg}\n{stack}");
                else if (type == LogType.Warning)
                    log.AppendLine($"Warning: {msg}");
            }

            Application.logMessageReceived += OnLog;
            bool ran;
            try
            {
                ran = EditorApplication.ExecuteMenuItem(menuPath);
            }
            catch (Exception e)
            {
                ran = false;
                log.AppendLine("Exception: " + e);
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }

            string head = !ran ? "FAILED (menu item not found or threw)" :
                log.ToString().Contains("Error") || log.ToString().Contains("Exception") ? "DONE WITH ERRORS" : "OK";
            File.WriteAllText(ResultFile, $"{head}: {menuPath}\n{log}");
        }
    }
}
