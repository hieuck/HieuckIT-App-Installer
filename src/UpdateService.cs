using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace HieuckIT_App_Installer
{
    public class UpdateService
    {
        private readonly UiLogger _logger;
        private static readonly HttpClient HttpClient = new HttpClient();

        public UpdateService(UiLogger logger)
        {
            _logger = logger;
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HieuckIT-App-Installer");
        }

        public async Task<string> CheckForAppListUpdatesAsync(string localPath, string onlineUrl)
        {
            _logger.Log("Checking for app list updates...", Color.Cyan);
            try
            {
                string localContent = File.Exists(localPath) ? await File.ReadAllTextAsync(localPath) : "";
                string onlineContent = await HttpClient.GetStringAsync(onlineUrl);

                if (localContent.Trim() != onlineContent.Trim())
                {
                    _logger.Log("A new version of the application list is available.", Color.Green);
                    var result = MessageBox.Show("A new application list is available. Do you want to update now?", "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (result == DialogResult.Yes)
                    {
                        await File.WriteAllTextAsync(localPath, onlineContent);
                        _logger.Log("App list updated successfully.", Color.Green);
                        return onlineContent; // Return new content to reload
                    }
                }
                else
                {
                    _logger.Log("App list is up-to-date.");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error checking for app list updates: {ex.Message}", Color.Red);
            }
            return null; // No update or failed
        }

        public async Task CheckForAppUpdateAsync(string currentVersion, string updateInfoUrl)
        {
            _logger.Log("Checking for program updates...", Color.Cyan);
            try
            {
                string json = await HttpClient.GetStringAsync(updateInfoUrl);
                JObject release = JObject.Parse(json);
                string latestVersionStr = release["tag_name"]?.ToString().TrimStart('v');
                string releaseUrl = release["html_url"]?.ToString();

                if (new Version(latestVersionStr) > new Version(currentVersion))
                {
                    _logger.Log($"New version available: {latestVersionStr}", Color.Green);
                    var result = MessageBox.Show($"A new version ({latestVersionStr}) is available. Do you want to go to the download page?", "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (result == DialogResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true });
                    }
                }
                else
                {
                    _logger.Log("You are on the latest version.");
                    MessageBox.Show("You are running the latest version.", "Up-to-Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error checking for program update: {ex.Message}", Color.Red);
                MessageBox.Show($"Error checking for update: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
