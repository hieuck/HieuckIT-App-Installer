using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using HieuckIT_App_Installer.Models; // Missing using directive added
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HieuckIT_App_Installer.Services
{
    public class AppConfigService
    {
        private readonly UiLogger _logger;
        private readonly IDeserializer _yamlDeserializer;

        public AppConfigService(UiLogger logger)
        {
            _logger = logger;
            _yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        public YamlRoot ParseYamlConfig(string yamlContent)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                _logger.Log("YAML content is empty, cannot parse.", Color.Red);
                return null;
            }
            try
            {
                return _yamlDeserializer.Deserialize<YamlRoot>(yamlContent);
            }
            catch (Exception ex)
            {
                _logger.Log($"Error parsing YAML: {ex.Message}", Color.Red);
                return null;
            }
        }

        public async Task<YamlRoot> LoadAppConfigAsync(string userConfigPath, string bundledConfigPath, string onlineUrl)
        {
            _logger.Log("Attempting to load YAML configuration...");
            string configContent = null;

            // 1. Prioritize user-specific config
            if (File.Exists(userConfigPath))
            {
                _logger.Log("Local YAML configuration loaded successfully.");
                configContent = File.ReadAllText(userConfigPath);
            }
            // 2. If not found, use the bundled config and copy it to the user path for future updates
            else if (File.Exists(bundledConfigPath))
            {
                _logger.Log("No user config found. Using bundled config and copying to AppData.");
                configContent = File.ReadAllText(bundledConfigPath);
                try
                {
                    File.WriteAllText(userConfigPath, configContent); 
                }
                catch (Exception ex)
                {
                    _logger.Log($"Warning: Could not write initial config to AppData: {ex.Message}", Color.Yellow);
                }
            }
            else
            {
                _logger.Log("Warning: No local or bundled config file found.", Color.Yellow);
            }
            
            if(configContent != null)
            {
                var config = ParseYamlConfig(configContent);
                if (config != null)
                {
                     _logger.Log($"Successfully parsed {config.Applications?.Count ?? 0} applications and {config.Utilities?.Count ?? 0} utilities.");
                     return config;
                }
            }
            
            _logger.Log("Falling back to downloading from URL as no valid local config was found.");
            string newContent = await DownloadAndUpdateLocalConfig(userConfigPath, onlineUrl);
            return ParseYamlConfig(newContent);
        }

        public async Task<string> DownloadAndUpdateLocalConfig(string localSavePath, string onlineUrl)
        {
             _logger.Log("Attempting to download latest app list...", Color.Yellow);
            if (string.IsNullOrEmpty(localSavePath) || string.IsNullOrEmpty(onlineUrl))
            {
                _logger.Log("Error: Local save path or online URL is not provided.", Color.Red);
                return null;
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "HieuckIT-App-Installer");
                    string newConfigContent = await client.GetStringAsync(onlineUrl);
                    if (!string.IsNullOrWhiteSpace(newConfigContent))
                    {
                        File.WriteAllText(localSavePath, newConfigContent);
                        _logger.Log("Successfully downloaded and updated local config.", Color.Green);
                        return newConfigContent;
                    }
                    _logger.Log("Downloaded config was empty.", Color.Yellow);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to download or save new config: {ex.Message}", Color.Red);
                return null;
            }
        }
    }
}
