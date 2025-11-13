
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Newtonsoft.Json.Linq;

// Final Version: v5.1 - Added Cleanup Option
public class InstallerForm : Form
{
    // --- CONFIGURATION ---
    private const string AppVersion = "5.1.0";
    private const string LocalYamlConfigPath = "apps.yaml";
    private const string DownloadFolderName = "Downloads";
    private const string OnlineYamlConfigUrl = "https://raw.githubusercontent.com/hieuck/HieuckIT-App-Installer/main/src/apps.yaml";
    private const string AppUpdateInfoUrl = "https://api.github.com/repos/hieuck/HieuckIT-App-Installer/releases/latest";
    private readonly string _downloadDirectory;
    // ---------------------

    #region UI_Components
    private CheckedListBox appListBox;
    private Button installButton;
    private Button updateAppsButton;
    private Button checkForAppUpdateButton;
    private CheckBox cleanupAfterInstallCheckBox;
    private RichTextBox logTextBox;
    private List<AppInfo> availableApps;
    private readonly bool is64BitOS = Environment.Is64BitOperatingSystem;
    #endregion

    public InstallerForm()
    {
        _downloadDirectory = Path.Combine(AppContext.BaseDirectory, DownloadFolderName);
        Directory.CreateDirectory(_downloadDirectory);
        InitializeComponent();
        Log($"Initializing HieuckIT App Installer...", Color.Cyan);
        Log($"Application Version: {AppVersion}");
        Log($"Download folder set to: {_downloadDirectory}");
        Log($"Detected OS: {(is64BitOS ? "64-bit" : "32-bit")}");
        LoadAppConfigAsync();
    }

    #region UI Initialization
    private void InitializeComponent()
    {
        this.Text = $"HieuckIT App Installer (v{AppVersion})";
        this.Size = new Size(800, 500);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        Label appLabel = new Label() { Text = "Available Applications", Location = new Point(10, 10), Size = new Size(250, 20), Font = new Font("Segoe UI", 9, FontStyle.Bold) };

        appListBox = new CheckedListBox
        {
            Location = new Point(10, 35),
            Size = new Size(250, 415),
            Enabled = false
        };

        installButton = new Button
        {
            Text = "Loading...",
            Location = new Point(270, 35),
            Size = new Size(250, 50),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Enabled = false
        };
        installButton.Click += InstallButton_Click;

        updateAppsButton = new Button
        {
            Text = "Update App List",
            Location = new Point(530, 35),
            Size = new Size(120, 50),
            Font = new Font("Segoe UI", 9),
            Enabled = true
        };
        updateAppsButton.Click += async (s, e) => await CheckForUpdatesAsync(true);

        checkForAppUpdateButton = new Button
        {
            Text = "Check for Updates",
            Location = new Point(655, 35),
            Size = new Size(120, 50),
            Font = new Font("Segoe UI", 9),
            Enabled = true
        };
        checkForAppUpdateButton.Click += CheckForAppUpdate_Click;


        Label logLabel = new Label() { Text = "Log", Location = new Point(270, 95), Size = new Size(100, 20), Font = new Font("Segoe UI", 9, FontStyle.Bold) };

        cleanupAfterInstallCheckBox = new CheckBox
        {
            Text = "Delete downloads after installation",
            Checked = false, // Default to keeping files
            Location = new Point(400, 95),
            AutoSize = true,
            Font = new Font("Segoe UI", 9)
        };

        logTextBox = new RichTextBox
        {
            Location = new Point(270, 120),
            Size = new Size(505, 330),
            ReadOnly = true,
            BackColor = Color.FromArgb(15, 15, 15),
            ForeColor = Color.FromArgb(0, 255, 0),
            Font = new Font("Consolas", 9)
        };

        this.Controls.Add(appLabel);
        this.Controls.Add(appListBox);
        this.Controls.Add(installButton);
        this.Controls.Add(updateAppsButton);
        this.Controls.Add(checkForAppUpdateButton);
        this.Controls.Add(logLabel);
        this.Controls.Add(cleanupAfterInstallCheckBox);
        this.Controls.Add(logTextBox);
    }
    #endregion

    #region Core Logic
    private async void LoadAppConfigAsync()
    {
        Log("Attempting to load local YAML configuration...", Color.Cyan);
        string configContent = null;

        if (File.Exists(LocalYamlConfigPath))
        {
            try
            {
                configContent = await Task.Run(() => File.ReadAllText(LocalYamlConfigPath));
                Log("Local YAML configuration loaded successfully.");
                await CheckForUpdatesAsync(false); // Check for updates in the background
            }
            catch (Exception ex)
            {
                Log($"Failed to read local YAML: {ex.Message}. Will try online.", Color.Yellow);
            }
        }
        
        if (configContent == null)
        {
            Log("No local config found. Downloading from repository...", Color.Cyan);
            configContent = await DownloadAndUpdateLocalConfig();
        }

        if (!string.IsNullOrEmpty(configContent))
        {
            ParseAndDisplayApps(configContent);
        }
        else
        {
            Log("FATAL: All configuration sources failed!", Color.Red);
            MessageBox.Show("Could not load application configuration.", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            appListBox.Enabled = false;
            installButton.Enabled = false;
            installButton.Text = "Failed!";
        }
    }
    
    private async Task<string> DownloadAndUpdateLocalConfig()
    {
        try
        {
            string onlineContent;
            using (WebClient client = new WebClient { Headers = { ["User-Agent"] = "Mozilla/5.0" } })
            {
                onlineContent = await client.DownloadStringTaskAsync(OnlineYamlConfigUrl);
            }
            
            await Task.Run(() => File.WriteAllText(LocalYamlConfigPath, onlineContent));
            Log("Downloaded and saved new local configuration.", Color.Green);
            return onlineContent;
        }
        catch (Exception ex)
        {
            Log($"Failed to download or save new config: {ex.Message}", Color.Red);
            return null;
        }
    }

    private void ParseAndDisplayApps(string yamlContent)
    {
        availableApps = ParseYamlConfig(yamlContent);
        if (availableApps != null && availableApps.Count > 0)
        {
            Log($"Loaded {availableApps.Count} applications from YAML.");
            PopulateAppList();
            appListBox.Enabled = true;
            installButton.Enabled = true;
            installButton.Text = "Install Selected";
        }
        else
        {
            Log("Failed to parse YAML or no applications found.", Color.Red);
        }
    }

    private async Task CheckForUpdatesAsync(bool userInitiated)
    {
        Log("Checking for app list updates...", Color.Cyan);
        try
        {
            string localContent = File.Exists(LocalYamlConfigPath) ? await Task.Run(() => File.ReadAllText(LocalYamlConfigPath)) : "";
            
            string onlineContent;
            using (WebClient client = new WebClient { Headers = { ["User-Agent"] = "Mozilla/5.0" } })
            {
                onlineContent = await client.DownloadStringTaskAsync(OnlineYamlConfigUrl);
            }

            if (localContent.Trim() != onlineContent.Trim())
            {
                Log("A new version of the application list is available.", Color.Green);
                var result = MessageBox.Show("A new application list is available. Do you want to update now?", "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                {
                    await Task.Run(() => File.WriteAllText(LocalYamlConfigPath, onlineContent));
                    Log("App list updated. Reloading...", Color.Green);
                    ParseAndDisplayApps(onlineContent);
                }
            }
            else
            {
                Log("App list is up-to-date.");
                if(userInitiated) MessageBox.Show("Your application list is already up-to-date.", "No Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            Log($"Error checking for updates: {ex.Message}", Color.Red);
            if(userInitiated) MessageBox.Show($"Error checking for updates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    
    private async void CheckForAppUpdate_Click(object sender, EventArgs e)
    {
        Log("Checking for program updates...", Color.Cyan);
        try
        {
            using (WebClient client = new WebClient { Headers = { ["User-Agent"] = "HieuckIT-App-Installer" } })
            {
                string json = await client.DownloadStringTaskAsync(AppUpdateInfoUrl);
                JObject release = JObject.Parse(json);
                string latestVersionStr = release["tag_name"]?.ToString().TrimStart('v');
                string releaseUrl = release["html_url"]?.ToString();

                if (new Version(latestVersionStr) > new Version(AppVersion))
                {
                    Log($"New version available: {latestVersionStr}", Color.Green);
                    var result = MessageBox.Show($"A new version ({latestVersionStr}) is available. Do you want to go to the download page?", "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (result == DialogResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true });
                    }
                }
                else
                {
                    Log("You are on the latest version.");
                    MessageBox.Show("You are running the latest version.", "Up-to-Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error checking for program update: {ex.Message}", Color.Red);
            MessageBox.Show($"Error checking for update: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private List<AppInfo> ParseYamlConfig(string yamlContent)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var root = deserializer.Deserialize<YamlRoot>(yamlContent);
            return root?.Applications ?? new List<AppInfo>();
        }
        catch (Exception ex)
        {
            Log($"YAML parsing error: {ex.Message}", Color.Red);
            return null;
        }
    }

    private void PopulateAppList()
    {
        if (this.InvokeRequired) { this.Invoke(new Action(PopulateAppList)); return; }
        if (availableApps == null) return;
        appListBox.Items.Clear();
        foreach (var app in availableApps)
        {
            appListBox.Items.Add(app.Name);
        }
    }

    private async void InstallButton_Click(object sender, EventArgs e)
    {
        installButton.Enabled = false;
        installButton.Text = "Installing...";
        logTextBox.Clear();

        List<AppInfo> selectedApps = new List<AppInfo>();
        foreach (var item in appListBox.CheckedItems)
        {
            var appInfo = availableApps.Find(app => app.Name == item.ToString());
            if (appInfo != null) selectedApps.Add(appInfo);
        }

        foreach (var appInfo in selectedApps)
        {
            await ProcessAppAsync(appInfo);
        }

        Log("------------------------------------------");
        Log("All selected applications have been processed.", Color.Cyan);
        installButton.Enabled = true;
        installButton.Text = "Install Selected";
    }

    private async Task ProcessAppAsync(AppInfo appInfo)
    {
        Log($"----- Starting process for {appInfo.Name} -----", Color.Yellow);

        KillProcess(appInfo.ProcessName);

        DownloadLink selectedLink = ChooseLink(appInfo.DownloadLinks);
        if (selectedLink == null) { Log($"Installation for {appInfo.Name} cancelled by user."); return; }

        string urlToDownload = is64BitOS ? selectedLink.Url_x64 : selectedLink.Url_x86;
        if (string.IsNullOrEmpty(urlToDownload))
        {
            Log($"No suitable download URL for {appInfo.Name} on this architecture. Skipping.", Color.OrangeRed);
            return;
        }

        string downloadedPath = await DownloadFileAsync(appInfo.Name, urlToDownload);
        if (string.IsNullOrEmpty(downloadedPath))
        {
            Log($"Skipping {appInfo.Name} due to download failure.", Color.Red);
            return;
        }

        if (appInfo.IsArchive)
        {
            string installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), appInfo.Name);
            await ExtractArchiveAsync(downloadedPath, $"x -y \"{downloadedPath}\" -o\"{installDir}\"", installDir);
        }
        else
        {
            await RunProcessAsync(downloadedPath, appInfo.InstallerArgs);
            Log($"{appInfo.Name} installation command executed.");
        }
        
        string patchPath = null;
        if (appInfo.PatchLinks != null && appInfo.PatchLinks.Count > 0)
        {
            patchPath = await ApplyPatchAsync(appInfo);
        }

        if (cleanupAfterInstallCheckBox.Checked)
        {
            Log("Cleanup enabled, deleting downloaded files...", Color.Gray);
            CleanupFile(downloadedPath);
            if(patchPath != null) CleanupFile(patchPath);
        }

        Log($"----- Finished process for {appInfo.Name} -----\n", Color.Yellow);
    }

    private async Task<string> ApplyPatchAsync(AppInfo appInfo)
    {
        Log($"Patch required for {appInfo.Name}. Locating install directory...");
        string installDir = GetInstallLocation(appInfo.RegistryDisplayName);

        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
        {
             Log($"Could not find install directory for '{appInfo.RegistryDisplayName}'. Skipping patch.", Color.OrangeRed);
             return null;
        }

        Log($"Found {appInfo.Name} at: {installDir}");

        DownloadLink patchLink = ChooseLink(appInfo.PatchLinks, "Choose Patch Source");
        if (patchLink == null) { Log("Patching cancelled by user."); return null; }

        string patchUrl = is64BitOS ? patchLink.Url_x64 : patchLink.Url_x86;
        if (string.IsNullOrEmpty(patchUrl)) { Log($"No suitable patch URL for this architecture.", Color.OrangeRed); return null; }

        string downloadedPatchPath = await DownloadFileAsync($"{appInfo.Name} Patch", patchUrl);
        if (string.IsNullOrEmpty(downloadedPatchPath)) { Log("Patch download failed. Skipping.", Color.Red); return null; }

        await ExtractArchiveAsync(downloadedPatchPath, appInfo.PatchArgs, installDir);
        return downloadedPatchPath; // Return path for potential cleanup
    }
    #endregion

    #region Helper & Utility Methods

    private void KillProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;
        try
        {
            string plainProcessName = Path.GetFileNameWithoutExtension(processName);
            Process[] processes = Process.GetProcessesByName(plainProcessName);
            if (processes.Length > 0)
            {
                Log($"Terminating {processes.Length} instance(s) of {processName}...", Color.Orange);
                foreach (var process in processes) { process.Kill(); }
                Log($"{processName} terminated.", Color.Orange);
            }
        }
        catch (Exception ex)
        {
            Log($"Error terminating process {processName}: {ex.Message}", Color.Red);
        }
    }

    private DownloadLink ChooseLink(List<DownloadLink> links, string prompt = "Choose Download Source")
    {
        if (links == null || links.Count == 0) return null;
        if (links.Count == 1) return links[0];

        using (Form choiceForm = new Form())
        {
            DownloadLink result = null;
            choiceForm.Text = prompt;
            choiceForm.Size = new Size(350, 150);
            choiceForm.StartPosition = FormStartPosition.CenterParent;
            choiceForm.FormBorderStyle = FormBorderStyle.FixedDialog;

            ListBox choiceBox = new ListBox() { Dock = DockStyle.Fill };
            foreach(var link in links) choiceBox.Items.Add(link.Name);
            choiceBox.SelectedIndex = 0;

            Button okButton = new Button() { Text = "OK", Dock = DockStyle.Bottom };
            okButton.Click += (s, e) => { 
                result = links[choiceBox.SelectedIndex];
                choiceForm.DialogResult = DialogResult.OK;
            };

            choiceForm.Controls.Add(choiceBox);
            choiceForm.Controls.Add(okButton);
            
            return (choiceForm.ShowDialog(this) == DialogResult.OK) ? result : null;
        }
    }
    
    private string GetInstallLocation(string displayName)
    {
        string[] registryKeys = {
            "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            "SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };
        foreach (var keyPath in registryKeys)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                if (key == null) continue;
                foreach (string subkeyName in key.GetSubKeyNames())
                {
                    using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                    {
                        var currentDisplayName = subkey?.GetValue("DisplayName") as string;
                        if (currentDisplayName != null && currentDisplayName.Contains(displayName))
                        {
                            return subkey.GetValue("InstallLocation") as string;
                        }
                    }
                }
            }
        }
        return null;
    }

    private async Task<string> DownloadFileAsync(string appName, string url)
    {
        Log($"Downloading {appName} from {url}...");
        try
        {
            using (WebClient client = new WebClient())
            {
                var uri = new Uri(url);
                string fileName = Path.GetFileName(uri.LocalPath);
                string filePath = Path.Combine(_downloadDirectory, fileName);

                Log($"File will be saved to: {filePath}");
                await client.DownloadFileTaskAsync(uri, filePath);
                Log($"{appName} downloaded successfully.", Color.Green);
                return filePath;
            }
        }
        catch (Exception ex)
        {
            Log($"Download failed for {appName}: {ex.Message}", Color.Red);
            return null;
        }
    }

    private async Task RunProcessAsync(string fileName, string args)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = true
            };
            Log($"Executing: \"{Path.GetFileName(fileName)}\" {args}");
            using (Process process = Process.Start(startInfo))
            {
                await Task.Run(() => process.WaitForExit()); 
                Log($"Process {Path.GetFileName(fileName)} finished.");
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to run process {fileName}: {ex.Message}", Color.Red);
        }
    }

    private void CleanupFile(string path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Log($"Cleaned up {Path.GetFileName(path)}.", Color.Gray);
            }
            catch (Exception ex)
            {
                Log($"Failed to cleanup file {path}: {ex.Message}", Color.Yellow);
            }
        }
    }

    private void Log(string message, Color? color = null)
    {
        if (logTextBox.InvokeRequired) { logTextBox.Invoke(new Action(() => Log(message, color))); return; }
        logTextBox.SelectionStart = logTextBox.TextLength;
        logTextBox.SelectionLength = 0;
        logTextBox.SelectionColor = color ?? Color.FromArgb(0, 255, 0);
        logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        logTextBox.ScrollToCaret();
    }
    #endregion

    #region Embedded Resource & 7-Zip Handling
    private async Task<string> Ensure7zExistsAsync()
    {
        string sevenZipDir = Path.Combine(AppContext.BaseDirectory, is64BitOS ? "7z" : "7z32");
        string sevenZipExe = Path.Combine(sevenZipDir, "7z.exe");

        if (File.Exists(sevenZipExe)) return sevenZipExe;
        
        Log($"7-Zip not found at {sevenZipDir}. This is an error in deployment.", Color.Red);
        MessageBox.Show("7-Zip is missing. Please ensure the 7z/ and 7z32/ folders are alongside the executable.", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return null;
    }

     private async Task ExtractArchiveAsync(string archivePath, string argumentPattern, string installDir)
    {
        string sevenZipExe = await Ensure7zExistsAsync();
        if (sevenZipExe == null) return;

        string finalArgs = argumentPattern
            .Replace("{patch_path}", archivePath)
            .Replace("{install_dir}", installDir);
        
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = sevenZipExe,
                Arguments = finalArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(sevenZipExe)
            };

            Log($"Extracting archive with args: {finalArgs}");
            using (Process process = Process.Start(startInfo))
            {
                await Task.Run(() => process.WaitForExit());
                string output = await process.StandardOutput.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(output)) Log($"7-Zip Output: {output}");
                Log("Extraction process finished.");
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to extract archive: {ex.Message}", Color.Red);
        }
    }

    #endregion
}

#region Data Models
public class YamlRoot
{
    public List<AppInfo> Applications { get; set; }
}

public class AppInfo
{
    public string Name { get; set; }
    public string ProcessName { get; set; }
    public string RegistryDisplayName { get; set; }
    public string InstallerArgs { get; set; }
    public bool IsArchive { get; set; }
    public List<DownloadLink> DownloadLinks { get; set; }
    public string PatchArgs { get; set; }
    public List<DownloadLink> PatchLinks { get; set; }
}

public class DownloadLink
{
    public string Name { get; set; }
    public string Url_x64 { get; set; }
    public string Url_x86 { get; set; }
}
#endregion
