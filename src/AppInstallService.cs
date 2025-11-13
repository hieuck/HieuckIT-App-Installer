
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HieuckIT_App_Installer.Models;
using Microsoft.Win32;

namespace HieuckIT_App_Installer
{
    public class AppInstallService
    {
        private readonly UiLogger _logger;
        private readonly string _tempDirectory;
        private readonly bool _is64BitOS;
        private static readonly HttpClient HttpClient = new HttpClient();

        public AppInstallService(UiLogger logger, string tempDirectory, bool is64BitOS)
        {
            _logger = logger;
            _tempDirectory = tempDirectory;
            _is64BitOS = is64BitOS;
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HieuckIT-App-Installer/2.0");
            Directory.CreateDirectory(_tempDirectory); // Ensure temp directory exists
        }

        #region Public Entry Points

        /// <summary>
        /// Processes a single standalone utility.
        /// </summary>
        public async Task ProcessUtilityAsync(Utility utility)
        {            
            _logger.Log($"----- Starting utility: {utility.Name} -----", Color.CornflowerBlue);
            var context = new Dictionary<string, string>();
            await ExecuteActionListAsync(utility.Actions, context, null); // Utilities don't have an associated application object
            _logger.Log($"----- Finished utility: {utility.Name} -----\n", Color.CornflowerBlue);
        }

        /// <summary>
        /// Processes the full installation, patch, and post-install for an application.
        /// </summary>
        public async Task ProcessApplicationAsync(Application app, bool cleanup)
        {
            _logger.Log($"----- Starting process for {app.Name} -----", Color.Yellow);

            var context = new Dictionary<string, string>
            {
                ["{temp_dir}"] = _tempDirectory,
                ["{version}"] = app.Version ?? ""
            };

            KillProcess(app.ProcessName); // Terminate before installation

            // Execute main installation steps
            _logger.Log($"[Phase 1/3] Running Install Steps for {app.Name}...", Color.Cyan);
            await ExecuteActionListAsync(app.InstallSteps, context, app);

            // Find install directory for subsequent steps
            string installDir = GetInstallLocation(app.RegistryDisplayName);
            if (!string.IsNullOrEmpty(installDir))
            {
                context["{install_dir}"] = installDir;
                _logger.Log($"Detected install directory: {installDir}", Color.Gray);
            }
            else
            {
                _logger.Log("Could not automatically detect install directory. Some actions may fail.", Color.Yellow);
            }
            
            // Execute patching steps
            _logger.Log($"[Phase 2/3] Running Patch Steps for {app.Name}...", Color.Cyan);
            await ExecuteActionListAsync(app.Patch, context, app);
            
            // Execute post-installation steps
            _logger.Log($"[Phase 3/3] Running Post-Install Steps for {app.Name}...", Color.Cyan);
            await ExecuteActionListAsync(app.PostInstall, context, app);

            if (cleanup)
            {
                CleanupTempDirectory();
            }

            _logger.Log($"----- Finished process for {app.Name} -----\n", Color.Yellow);
        }

        #endregion

        #region Core Action Executor

        /// <summary>
        /// Iterates through a list of actions and executes them sequentially.
        /// </summary>
        private async Task ExecuteActionListAsync(List<InstallAction> actions, Dictionary<string, string> context, Application app)
        {
            if (actions == null || !actions.Any()) return;

            foreach (var action in actions)
            {
                // Resolve placeholders in all string properties of the action before execution
                ResolveAllPlaceholders(action, context);

                if (action.RequiresAdmin && !Program.IsAdministrator())
                {
                    _logger.Log($"Action '{action.Type}' requires Admin rights. Please restart the application as Administrator. Skipping.", Color.Red);
                    continue;
                }

                try
                {
                    switch (action.Type)
                    {
                        case ActionType.DownloadAndRun: await HandleDownloadAndRunAsync(action, context, app); break;
                        case ActionType.Download: await HandleDownloadAsync(action, context); break;
                        case ActionType.Extract: await HandleExtractAsync(action); break;
                        case ActionType.RunCommand: await HandleRunCommandAsync(action); break;
                        case ActionType.RunScript: await HandleRunScriptAsync(action); break;
                        case ActionType.OpenFile: HandleOpenFile(action); break;
                        case ActionType.CreateShortcut: await HandleCreateShortcutAsync(action); break;
                        default: _logger.Log($"Unknown action type: {action.Type}. Skipping.", Color.Yellow); break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"ERROR executing action {action.Type} for {app?.Name ?? "Utility"}: {ex.Message}", Color.Red);
                    // Optionally, decide if the entire process should stop on failure.
                }
            }
        }

        #endregion

        #region Action Handlers

        private async Task HandleDownloadAndRunAsync(InstallAction action, Dictionary<string, string> context, Application app)
        {
            if (app == null) return;
            string url = ChooseDownloadUrl(app.DownloadLinks);
            if (string.IsNullOrEmpty(url)) return;

            string downloadedPath = await DownloadFileAsync(app.Name, url);
            if (!string.IsNullOrEmpty(downloadedPath))
            {
                context["{downloaded_file_path}"] = downloadedPath;
                await RunProcessAsync(downloadedPath, action.Args, wait: true);
            }
        }

        private async Task HandleDownloadAsync(InstallAction action, Dictionary<string, string> context)
        {
            string url = action.Url;
            if (string.IsNullOrEmpty(url))
            {                
                 _logger.Log("Download action has no URL. Skipping.", Color.Yellow);
                 return;
            }

            string downloadedPath = await DownloadFileAsync(action.FileName ?? "file", url);
            if (!string.IsNullOrEmpty(downloadedPath))
            {
                context["{downloaded_file_path}"] = downloadedPath;
            }
        }

        private async Task HandleExtractAsync(InstallAction action)
        {
            string sevenZipExe = Ensure7zExists();
            if (sevenZipExe == null || string.IsNullOrEmpty(action.Archive))
            {
                _logger.Log("7-Zip not found or no archive specified. Skipping extraction.", Color.Red);
                return;
            }
            await RunProcessAsync(sevenZipExe, $"x \"{action.Archive}\" -o\"{action.Destination}\" {action.Args}", wait: true);
        }

        private async Task HandleRunCommandAsync(InstallAction action)
        {
            var parts = action.Command.Split(new[] { ' ' }, 2);
            string fileName = parts[0];
            string args = parts.Length > 1 ? parts[1] : "";
            await RunProcessAsync(fileName, args, wait: true);
        }

        private async Task HandleRunScriptAsync(InstallAction action)
        {
            await RunProcessAsync(action.Path, action.Args, wait: true, useShellExecute: true);
        }

        private void HandleOpenFile(InstallAction action)
        {
            if (string.IsNullOrEmpty(action.Path))
            {
                 _logger.Log("OpenFile action has no Path specified. Skipping.", Color.Yellow);
                 return;
            }
            _logger.Log($"Opening: {action.Path}");
            Process.Start(new ProcessStartInfo(action.Path) { UseShellExecute = true });
        }

        private async Task HandleCreateShortcutAsync(InstallAction action)
        {
            _logger.Log($"Creating shortcut: {action.ShortcutName}");
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktopPath, $"{action.ShortcutName}.lnk");
            
            string psCommand = $"-Command \"$WshShell = New-Object -ComObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut(''{shortcutPath}''); $Shortcut.TargetPath = ''{action.Target}''; $Shortcut.Save()\"";
            await RunProcessAsync("powershell.exe", psCommand, wait: true);
        }

        #endregion

        #region Helper & Utility Methods

        private async Task<string> DownloadFileAsync(string appName, string url)
        {
            _logger.Log($"Downloading {appName} from {url}...");
            try
            {
                string fileName = Path.GetFileName(new Uri(url).LocalPath);
                string filePath = Path.Combine(_tempDirectory, fileName);

                using (var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fs);
                    }
                }
                _logger.Log($"Download successful: {filePath}", Color.Green);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.Log($"Download failed for {appName}: {ex.Message}", Color.Red);
                return null;
            }
        }

        private async Task RunProcessAsync(string fileName, string args, bool wait = false, string workingDir = null, bool useShellExecute = false)
        {
            _logger.Log($"Executing: \"{fileName}\" {args}");
            try
            {
                var startInfo = new ProcessStartInfo(fileName, args)
                {
                    UseShellExecute = useShellExecute, // Must be true for UAC elevation on .bat, etc.
                    RedirectStandardOutput = !useShellExecute,
                    RedirectStandardError = !useShellExecute,
                    CreateNoWindow = !useShellExecute,
                    WorkingDirectory = workingDir
                };

                using (var process = Process.Start(startInfo))
                {
                    if (wait && !useShellExecute)
                    {
                        string output = await process.StandardOutput.ReadToEndAsync();
                        string error = await process.StandardError.ReadToEndAsync();
                        await Task.Run(() => process.WaitForExit()); // Use Task.Run to be truly async
                        if (!string.IsNullOrWhiteSpace(output)) _logger.Log($"Output: {output}", Color.Gray);
                        if (!string.IsNullOrWhiteSpace(error)) _logger.Log($"Error: {error}", Color.DarkRed);
                    }
                    else if (wait)
                    {
                        await Task.Run(() => process.WaitForExit());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to run process \"{fileName}\": {ex.Message}", Color.Red);
            }
        }
        
        private string ChooseDownloadUrl(List<DownloadSource> sources)
        {
            // This is a placeholder. In a real GUI app, this would be a user prompt.
            // For now, just take the first valid URL.
            if (sources == null || !sources.Any()) return null;

            var firstTarget = sources.First().Targets?.FirstOrDefault();
            if (firstTarget == null) return null;

            return _is64BitOS ? firstTarget.Url_x64 : firstTarget.Url_x86;
        }

        private void KillProcess(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return;
            try
            {
                string name = Path.GetFileNameWithoutExtension(processName);
                foreach (var process in Process.GetProcessesByName(name))
                {
                    _logger.Log($"Terminating existing process: {process.ProcessName} (ID: {process.Id})", Color.Orange);
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to kill process {processName}: {ex.Message}", Color.Yellow);
            }
        }

        private string GetInstallLocation(string displayName)
        {
            string[] keys = { 
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
                "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"
            };
            foreach (var keyPath in keys)
            {
                using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key == null) continue;
                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        using (var subkey = key.OpenSubKey(subkeyName))
                        {
                            if (subkey?.GetValue("DisplayName") as string == displayName)
                            {
                                return subkey.GetValue("InstallLocation") as string;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private void CleanupTempDirectory()
        {
            _logger.Log("Cleaning up temporary directory...", Color.Gray);
            try
            {
                var dirInfo = new DirectoryInfo(_tempDirectory);
                foreach (var file in dirInfo.GetFiles()) file.Delete();
                foreach (var dir in dirInfo.GetDirectories()) dir.Delete(true);
                _logger.Log("Cleanup complete.", Color.Gray);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed during cleanup: {ex.Message}", Color.Yellow);
            }
        }

        private void ResolveAllPlaceholders(InstallAction action, IReadOnlyDictionary<string, string> context)
        {
            if (action == null || context == null) return;
            action.Command = ResolvePlaceholders(action.Command, context);
            action.Path = ResolvePlaceholders(action.Path, context);
            action.Args = ResolvePlaceholders(action.Args, context);
            action.Target = ResolvePlaceholders(action.Target, context);
            action.ShortcutName = ResolvePlaceholders(action.ShortcutName, context);
            action.Url = ResolvePlaceholders(action.Url, context);
            action.FileName = ResolvePlaceholders(action.FileName, context);
            action.Archive = ResolvePlaceholders(action.Archive, context);
            action.Destination = ResolvePlaceholders(action.Destination, context);
        }

        private string ResolvePlaceholders(string input, IReadOnlyDictionary<string, string> context)
        {
            if (string.IsNullOrEmpty(input) || context == null) return input;
            foreach (var entry in context)
            {
                input = input.Replace(entry.Key, entry.Value);
            }
            return input;
        }

        private string Ensure7zExists()
        {
            string sevenZipPath = Path.Combine(AppContext.BaseDirectory, _is64BitOS ? "7z/7z.exe" : "7z32/7z.exe");
            if (File.Exists(sevenZipPath)) return sevenZipPath;

            _logger.Log("FATAL: 7z.exe not found. It must be included with the application.", Color.Red);
            // In a real app, you might try to download it.
            return null;
        }
        #endregion
    }
}
