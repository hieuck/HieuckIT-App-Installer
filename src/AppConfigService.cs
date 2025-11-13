
using System;
using System.Collections.Generic;
using System.Drawing; // Required for the Color enum in UiLogger
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using HieuckIT_App_Installer.Models; // <-- IMPORT THE NEW MODELS
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HieuckIT_App_Installer
{
    public class AppConfigService
    {
        private readonly UiLogger _logger;
        private static readonly HttpClient HttpClient = new HttpClient();

        public AppConfigService(UiLogger logger)
        {
            _logger = logger;
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HieuckIT-App-Installer/2.0");
        }

        // The method now returns the entire YamlRoot object
        public async Task<YamlRoot> LoadAppConfigAsync(string localPath, string onlineUrl)
        {
            _logger.Log("Attempting to load local YAML configuration...", Color.Cyan);
            string configContent = null;

            if (File.Exists(localPath))
            {
                try
                {
                    configContent = await File.ReadAllTextAsync(localPath);
                    _logger.Log("Local YAML configuration loaded successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Failed to read local YAML: {ex.Message}. Will try online.", Color.Yellow);
                }
            }

            if (string.IsNullOrEmpty(configContent))
            {
                _logger.Log("No local config found or it failed to load. Downloading from repository...", Color.Cyan);
                configContent = await DownloadAndUpdateLocalConfig(localPath, onlineUrl);
            }

            if (!string.IsNullOrEmpty(configContent))
            {
                return ParseYamlConfig(configContent);
            }

            _logger.Log("FATAL: Failed to load or download any configuration. The application cannot proceed.", Color.Red);
            return null; // Return null if everything fails
        }

        public async Task<string> DownloadAndUpdateLocalConfig(string localPath, string onlineUrl)
        {
            try
            {
                string onlineContent = await HttpClient.GetStringAsync(onlineUrl);
                // Ensure the directory exists before writing the file.
                Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                await File.WriteAllTextAsync(localPath, onlineContent);
                _logger.Log("Downloaded and saved new local configuration.", Color.Green);
                return onlineContent;
            }
            catch (HttpRequestException ex)
            {
                _logger.Log($"HTTP request failed: {ex.Message}", Color.Red);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to download or save new config: {ex.Message}", Color.Red);
                return null;
            }
        }

        // This method now deserializes the YAML into a YamlRoot object
        public YamlRoot ParseYamlConfig(string yamlContent)
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties() // Important for forward compatibility
                    .Build();

                var root = deserializer.Deserialize<YamlRoot>(yamlContent);

                if (root != null)
                {
                    // Prevent null reference exceptions in the UI later
                    root.Applications ??= new List<Application>();
                    root.Utilities ??= new List<Utility>();
                    _logger.Log($"Successfully parsed {root.Applications.Count} applications and {root.Utilities.Count} utilities.", Color.Green);
                }
                else
                {
                    _logger.Log("YAML was parsed, but the root object is null. The file might be empty or malformed.", Color.Yellow);
                }

                return root;
            }
            catch (Exception ex)
            {
                _logger.Log($"FATAL YAML PARSING ERROR: {ex.Message}", Color.Red);
                if (ex.InnerException != null)
                {                    
                    _logger.Log($" -> Inner Exception: {ex.InnerException.Message}", Color.Red);
                }
                return null;
            }
        }
    }
}
