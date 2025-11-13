
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HieuckIT_App_Installer.Models; // Use the new models namespace

namespace HieuckIT_App_Installer
{
    public class InstallerForm : Form
    {
        // --- Configuration ---
        private const string AppVersion = "6.0.0-beta"; // Version indicating major refactor
        private const string LocalYamlConfigPath = "apps.yaml";
        private const string DownloadFolderName = "Downloads";
        private const string OnlineYamlConfigUrl = "https://raw.githubusercontent.com/hieuck/HieuckIT-App-Installer/main/apps.yaml";
        private const string AppUpdateInfoUrl = "https://api.github.com/repos/hieuck/HieuckIT-App-Installer/releases/latest";
        private readonly string _tempDirectory;

        // --- UI Components ---
        private TreeView itemTreeView; // Changed from CheckedListBox to TreeView
        private Button installButton;
        private Button updateAppsButton;
        private Button checkForAppUpdateButton;
        private CheckBox cleanupAfterInstallCheckBox;
        private RichTextBox logTextBox;

        // --- Services & Data ---
        private YamlRoot _appConfig; // Changed to hold the entire YAML structure
        private readonly bool _is64BitOS = Environment.Is64BitOperatingSystem;
        private readonly UiLogger _logger;
        private readonly AppConfigService _configService;
        private readonly AppInstallService _installService;
        private readonly UpdateService _updateService;

        public InstallerForm()
        {
            _tempDirectory = Path.Combine(AppContext.BaseDirectory, DownloadFolderName);
            Directory.CreateDirectory(_tempDirectory);

            InitializeComponent();

            _logger = new UiLogger(logTextBox);
            _configService = new AppConfigService(_logger);
            _installService = new AppInstallService(_logger, _tempDirectory, _is64BitOS);
            _updateService = new UpdateService(_logger);

            _logger.Log($"Initializing HieuckIT App Installer...", Color.Cyan);
            _logger.Log($"Application Version: {AppVersion}");
            _logger.Log($"Temporary download folder: {_tempDirectory}");
            _logger.Log($"Detected OS: {(_is64BitOS ? "64-bit" : "32-bit")}");

            LoadInitialAppConfigAsync();
        }

        #region UI Initialization
        private void InitializeComponent()
        {
            this.Text = $"HieuckIT App Installer (v{AppVersion})";
            this.Size = new Size(800, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            Label appLabel = new Label() { Text = "Available Software & Utilities", Location = new Point(10, 10), Size = new Size(250, 20), Font = new Font("Segoe UI", 9, FontStyle.Bold) };

            itemTreeView = new TreeView
            {
                Location = new Point(10, 35),
                Size = new Size(250, 415),
                CheckBoxes = true, // Enable checkboxes for selection
                Enabled = false,
                Font = new Font("Segoe UI", 9.5f)
            };

            installButton = new Button { Text = "Loading...", Location = new Point(270, 35), Size = new Size(250, 50), Font = new Font("Segoe UI", 12, FontStyle.Bold), Enabled = false };
            installButton.Click += InstallButton_Click;

            updateAppsButton = new Button { Text = "Update List", Location = new Point(530, 35), Size = new Size(120, 50), Font = new Font("Segoe UI", 9) };
            updateAppsButton.Click += UpdateAppsButton_Click;

            checkForAppUpdateButton = new Button { Text = "Check for Updates", Location = new Point(655, 35), Size = new Size(120, 50), Font = new Font("Segoe UI", 9) };
            checkForAppUpdateButton.Click += CheckForAppUpdate_Click;
            
            Label logLabel = new Label() { Text = "Log", Location = new Point(270, 95), Size = new Size(100, 20), Font = new Font("Segoe UI", 9, FontStyle.Bold) };

            cleanupAfterInstallCheckBox = new CheckBox { Text = "Delete downloads after install", Checked = true, Location = new Point(530, 95), AutoSize = true, Font = new Font("Segoe UI", 9) };

            logTextBox = new RichTextBox { Location = new Point(270, 120), Size = new Size(505, 330), ReadOnly = true, BackColor = Color.FromArgb(15, 15, 15), ForeColor = Color.FromArgb(0, 255, 0), Font = new Font("Consolas", 9) };

            this.Controls.AddRange(new Control[] { appLabel, itemTreeView, installButton, updateAppsButton, checkForAppUpdateButton, logLabel, cleanupAfterInstallCheckBox, logTextBox });
        }
        #endregion

        #region Core Logic & Event Handlers

        private async void LoadInitialAppConfigAsync()
        {
            _appConfig = await _configService.LoadAppConfigAsync(LocalYamlConfigPath, OnlineYamlConfigUrl);
            if (_appConfig != null)
            {
                PopulateItemTree();
                EnableControls();
                await _updateService.CheckForAppListUpdatesAsync(LocalYamlConfigPath, OnlineYamlConfigUrl); // Background check
            }
            else
            {
                _logger.Log("FATAL: Application configuration could not be loaded!", Color.Red);
                MessageBox.Show("Could not load application configuration.", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                installButton.Text = "Failed!";
            }
        }

        private void PopulateItemTree()
        {
            if (this.InvokeRequired) { this.Invoke(new Action(PopulateItemTree)); return; }

            itemTreeView.Nodes.Clear();
            if (_appConfig == null) return;

            TreeNode appNode = new TreeNode("Applications");
            if (_appConfig.Applications != null)
            {
                foreach (var app in _appConfig.Applications.OrderBy(a => a.Name))
                {
                    appNode.Nodes.Add(new TreeNode(app.Name) { Tag = app });
                }
            }

            TreeNode utilityNode = new TreeNode("Utilities");
            if (_appConfig.Utilities != null)
            {
                foreach (var util in _appConfig.Utilities.OrderBy(u => u.Name))
                {
                    utilityNode.Nodes.Add(new TreeNode(util.Name) { Tag = util });
                }
            }

            itemTreeView.Nodes.Add(appNode);
            itemTreeView.Nodes.Add(utilityNode);
            itemTreeView.ExpandAll();
        }

        private void EnableControls()
        {
            itemTreeView.Enabled = true;
            installButton.Enabled = true;
            installButton.Text = "Process Selected";
        }

        private async void InstallButton_Click(object sender, EventArgs e)
        {
            installButton.Enabled = false;
            installButton.Text = "Processing...";
            logTextBox.Clear();

            var checkedNodes = GetCheckedNodes(itemTreeView.Nodes);
            if (!checkedNodes.Any())
            {
                 _logger.Log("No items selected.", Color.Yellow);
                 EnableControls();
                 return;
            }

            foreach (TreeNode node in checkedNodes)
            {
                if (node.Tag is Application app)
                {
                    await _installService.ProcessApplicationAsync(app, cleanupAfterInstallCheckBox.Checked);
                }
                else if (node.Tag is Utility util)
                {
                    await _installService.ProcessUtilityAsync(util);
                }
            }

            _logger.Log("------------------------------------------");
            _logger.Log("All selected tasks have been processed.", Color.Cyan);
            EnableControls();
        }
        
        private List<TreeNode> GetCheckedNodes(TreeNodeCollection nodes)
        {
            List<TreeNode> checkedNodes = new List<TreeNode>();
            foreach(TreeNode node in nodes)
            {
                if(node.Checked && node.Tag != null) { checkedNodes.Add(node); }
                checkedNodes.AddRange(GetCheckedNodes(node.Nodes));
            }
            return checkedNodes;
        }

        private async void UpdateAppsButton_Click(object sender, EventArgs e)
        {
            string newConfigContent = await _configService.DownloadAndUpdateLocalConfig(LocalYamlConfigPath, OnlineYamlConfigUrl);
            if (!string.IsNullOrEmpty(newConfigContent))
            {
                // Reparse the new content and repopulate the tree
                _appConfig = _configService.ParseYamlConfig(newConfigContent);
                PopulateItemTree();
            }
        }

        private async void CheckForAppUpdate_Click(object sender, EventArgs e)
        {
            await _updateService.CheckForAppUpdateAsync(AppVersion, AppUpdateInfoUrl);
        }

        #endregion
    }
}
