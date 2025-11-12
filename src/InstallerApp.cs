using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Reflection;

// Final Version: v4.0 - Ultimate (Architecture-aware, Multi-link)
public class InstallerForm : Form
{
    // --- CONFIGURATION ---
    private const string OnlineConfigUrl = "https://raw.githubusercontent.com/hieuck/curl-uri-wget-download-setup/main/apps.json";
    private const string FallbackConfigResource = "apps.json";
    private const string SevenZipExeResource = "7z.exe";
    private const string SevenZipDllResource = "7z.dll";
    // ---------------------

    #region UI_Components
    private CheckedListBox appListBox;
    private Button installButton;
    private RichTextBox logTextBox;
    private List<AppInfo> availableApps;
    private readonly bool is64BitOS = Environment.Is64BitOperatingSystem;
    #endregion

    public InstallerForm()
    {
        InitializeComponent();
        Log("Initializing HieuckIT App Installer...", Color.Cyan);
        Log($"Detected OS: {(is64BitOS ? "64-bit" : "32-bit")}");
        LoadAppConfigAsync();
    }

    #region UI Initialization
    private void InitializeComponent()
    {
        this.Text = "HieuckIT App Installer (v4.0 - Ultimate)";
        this.Size = new Size(700, 500);
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
            Size = new Size(200, 50),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Enabled = false
        };
        installButton.Click += InstallButton_Click;

        Label logLabel = new Label() { Text = "Log", Location = new Point(270, 95), Size = new Size(100, 20), Font = new Font("Segoe UI", 9, FontStyle.Bold) };

        logTextBox = new RichTextBox
        {
            Location = new Point(270, 120),
            Size = new Size(400, 330),
            ReadOnly = true,
            BackColor = Color.FromArgb(15, 15, 15),
            ForeColor = Color.FromArgb(0, 255, 0),
            Font = new Font("Consolas", 9)
        };

        this.Controls.Add(appLabel);
        this.Controls.Add(appListBox);
        this.Controls.Add(installButton);
        this.Controls.Add(logLabel);
        this.Controls.Add(logTextBox);
    }
    #endregion

    #region Core Logic
    private async void LoadAppConfigAsync()
    {
        string jsonContent = null;
        Log("Attempting to download latest configuration...", Color.Cyan);

        try
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", "Mozilla/5.0");
                jsonContent = await client.DownloadStringTaskAsync(OnlineConfigUrl);
                Log("Latest configuration downloaded successfully.");
            }
        }
        catch (Exception ex)
        {
            Log($"Online config failed: {ex.Message}. Using fallback.", Color.Yellow);
            jsonContent = ReadEmbeddedResource(FallbackConfigResource);
             if (jsonContent == null)
            {
                 Log("FATAL: Fallback configuration is missing!", Color.Red);
                 MessageBox.Show("Application is corrupted!", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                 return;
            }
        }

        try
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            availableApps = serializer.Deserialize<List<AppInfo>>(jsonContent);
            Log($"Loaded {availableApps.Count} applications.");
            PopulateAppList();
        }
        catch (Exception ex)
        {
            Log($"Error parsing configuration: {ex.Message}", Color.Red);
        }
        finally
        {
            appListBox.Enabled = true;
            installButton.Enabled = true;
            installButton.Text = "Install Selected";
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

        // 1. CHOOSE DOWNLOAD LINK
        DownloadLink selectedLink = ChooseLink(appInfo.DownloadLinks);
        if (selectedLink == null) { Log($"Installation for {appInfo.Name} cancelled by user."); return; }

        string urlToDownload = is64BitOS ? selectedLink.Url_x64 : selectedLink.Url_x86;
        if (string.IsNullOrEmpty(urlToDownload))
        {
            Log($"No suitable download URL for {appInfo.Name} on this architecture. Skipping.", Color.OrangeRed);
            return;
        }

        // 2. DOWNLOAD
        string installerPath = await DownloadFileAsync(appInfo.Name, urlToDownload);
        if (string.IsNullOrEmpty(installerPath))
        {
            Log($"Skipping {appInfo.Name} due to download failure.", Color.Red);
            return;
        }

        // 3. INSTALL
        await RunProcessAsync(installerPath, appInfo.InstallerArgs);
        Log($"{appInfo.Name} installation command executed.");

        // 4. PATCH
        if (appInfo.PatchLinks != null && appInfo.PatchLinks.Count > 0)
        {
            await ApplyPatchAsync(appInfo);
        }

        // 5. CLEANUP
        CleanupFile(installerPath);
        Log($"----- Finished process for {appInfo.Name} -----\n", Color.Yellow);
    }

    private async Task ApplyPatchAsync(AppInfo appInfo)
    {
        Log($"Patch required for {appInfo.Name}. Locating install directory...");
        string installDir = GetInstallLocation(appInfo.RegistryDisplayName);

        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
        {
             Log($"Could not find install directory for '{appInfo.RegistryDisplayName}'. Skipping patch.", Color.OrangeRed);
             return;
        }

        Log($"Found {appInfo.Name} at: {installDir}");

        DownloadLink patchLink = ChooseLink(appInfo.PatchLinks, "Choose Patch Source");
        if (patchLink == null) { Log("Patching cancelled by user."); return; }

        string patchUrl = is64BitOS ? patchLink.Url_x64 : patchLink.Url_x86;
        if (string.IsNullOrEmpty(patchUrl)) { Log($"No suitable patch URL for this architecture.", Color.OrangeRed); return; }

        string patchPath = await DownloadFileAsync($"{appInfo.Name} Patch", patchUrl);
        if (string.IsNullOrEmpty(patchPath)) { Log("Patch download failed. Skipping.", Color.Red); return; }

        await ExtractArchiveAsync(patchPath, appInfo.PatchArgs, installDir);
        CleanupFile(patchPath);
    }
    #endregion

    #region Helper & Utility Methods

    private DownloadLink ChooseLink(List<DownloadLink> links, string prompt = "Choose Download Source")
    {
        if (links == null || links.Count == 0) return null;
        if (links.Count == 1) return links[0];

        // Create a form on the fly to ask the user
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
            "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
            "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"
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
                string extension = Path.GetExtension(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(extension) || extension.Length > 5) extension = ".tmp";
                string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");

                await client.DownloadFileTaskAsync(new Uri(url), filePath);
                Log($"{appName} downloaded successfully.");
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
            try { File.Delete(path); Log($"Cleaned up {Path.GetFileName(path)}."); }
            catch (Exception ex) { Log($"Failed to cleanup file {path}: {ex.Message}", Color.Yellow); }
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

    private string ReadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePath = resourceName;

        if (Array.Find(assembly.GetManifestResourceNames(), name => name.EndsWith(resourceName)) is string foundName) 
        {
             resourcePath = foundName;
        } 
        
        using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
        {
            if (stream == null) return null;
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }

    private bool ExtractEmbeddedResource(string resourceName, string outputPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePath = resourceName;
        
        if (Array.Find(assembly.GetManifestResourceNames(), name => name.EndsWith(resourceName)) is string foundName) 
        { 
            resourcePath = foundName;
        }

        using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
        {
            if (stream == null) return false;
            using (FileStream fileStream = new FileStream(outputPath, FileMode.Create))
            {
                stream.CopyTo(fileStream);
            }
        }
        return true;
    }

    private async Task<string> Ensure7zExistsAsync()
    {
        string sevenZipPath = Path.Combine(Path.GetTempPath(), SevenZipExeResource);
        if (File.Exists(sevenZipPath)) return sevenZipPath;

        Log("7-Zip not found. Extracting from embedded resource...", Color.Cyan);
        string sevenZipDllPath = Path.Combine(Path.GetTempPath(), SevenZipDllResource);

        bool successExe = await Task.Run(() => ExtractEmbeddedResource(SevenZipExeResource, sevenZipPath));
        bool successDll = await Task.Run(() => ExtractEmbeddedResource(SevenZipDllResource, sevenZipDllPath));

        if (successExe && successDll)
        {
            Log("7-Zip extracted successfully.");
            return sevenZipPath;
        }
        else
        {
            Log("FATAL: Could not extract 7-Zip from EXE.", Color.Red);
            return null;
        }
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
                WorkingDirectory = Path.GetTempPath()
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

    [STAThread]
    public static void Main()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }
}

#region Data Models
public class AppInfo
{
    public string Name { get; set; }
    public string RegistryDisplayName { get; set; }
    public string InstallerArgs { get; set; }
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
