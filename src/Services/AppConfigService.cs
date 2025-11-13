using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using HieuckIT_App_Installer.Models;
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
            if (string.IsNullOrWhiteSpace(yamlContent)) return null;
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

        public async Task<YamlRoot> LoadAppConfigAsync(string localConfigPath, string onlineUrl)
        {
            string configContent = null;
            // Priority 1: Try to load the config file from the application's directory.
            if (File.Exists(localConfigPath))
            {
                _logger.Log("Local config file found.", Color.Lime);
                configContent = await File.ReadAllTextAsync(localConfigPath);
            }
            
            // If we have content, try to parse it.
            if (!string.IsNullOrEmpty(configContent))
            {
                var config = ParseYamlConfig(configContent);
                if (config != null)
                {
                    _logger.Log($"Successfully parsed {config.Applications?.Count ?? 0} applications and {config.Utilities?.Count ?? 0} utilities.", Color.Green);
                    return config;
                }
                _logger.Log("Local config was found but could not be parsed. It might be corrupted.", Color.Yellow);
            }

            // Priority 2: If no local file or parsing failed, download from the web.
            _logger.Log("Falling back to downloading from repository...", Color.Cyan);
            string newContent = await DownloadAndUpdateLocalConfig(localConfigPath, onlineUrl);
            return ParseYamlConfig(newContent);
        }

        public async Task<string> DownloadAndUpdateLocalConfig(string localSavePath, string onlineUrl)
        {
            _logger.Log($"Downloading latest app list to: {localSavePath}", Color.Yellow);
            if (string.IsNullOrEmpty(localSavePath) || string.IsNullOrEmpty(onlineUrl))
            {
                _logger.Log("FATAL: Local save path or online URL is not provided.", Color.Red);
                return null;
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("HieuckIT-App-Installer");
                    string newConfigContent = await client.GetStringAsync(onlineUrl);

                    if (!string.IsNullOrWhiteSpace(newConfigContent))
                    {
                        // This will now save the apps.yaml file directly next to the .exe
                        await File.WriteAllTextAsync(localSavePath, newConfigContent);
                        _logger.Log("Successfully downloaded and updated the local config file.", Color.Green);
                        return newConfigContent;
                    }
                    else
                    {
                        _logger.Log("Downloaded config was empty. The local file was not updated.", Color.Yellow);
                        return null;
                    }
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
