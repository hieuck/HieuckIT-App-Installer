
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace HieuckIT_App_Installer.Models
{
    // Cấu trúc gốc của tệp YAML, chứa cả ứng dụng và tiện ích
    public class YamlRoot
    {
        public List<Application> Applications { get; set; }
        public List<Utility> Utilities { get; set; }
    }

    // --- A. Phần Cài đặt Ứng dụng ---

    public class Application
    {
        public string Name { get; set; }
        public string ProcessName { get; set; }
        public string RegistryDisplayName { get; set; }
        public string Version { get; set; }

        // Các khối xử lý thống nhất sử dụng cùng một hệ thống Action
        public List<DownloadSource> DownloadLinks { get; set; }
        public List<InstallAction> InstallSteps { get; set; }
        public List<InstallAction> Patch { get; set; }
        public List<InstallAction> PostInstall { get; set; }
    }

    public class DownloadSource
    {
        public string Name { get; set; }
        public bool ConvertDirectLink { get; set; } = false;
        public List<DownloadTarget> Targets { get; set; }
    }

    public class DownloadTarget
    {
        public string OSVersion { get; set; } = "Default";
        public string Url_x64 { get; set; }
        public string Url_x86 { get; set; }
    }

    // --- B. Phần Tiện ích & Công cụ ---

    public class Utility
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<InstallAction> Actions { get; set; }
    }

    // --- C. Hệ thống Hành động (Action) chung ---
    // Được sử dụng cho cả InstallSteps, Patch, PostInstall, và Utilities

    public class InstallAction
    {
        [YamlMember(Alias = "Type", ApplyNamingConventions = false)]
        public ActionType Type { get; set; }
        public bool RequiresAdmin { get; set; } = false;

        // Thuộc tính cho các loại hành động khác nhau
        public string Command { get; set; }      // For RunCommand
        public string Path { get; set; }         // For OpenFile, RunScript
        public string Args { get; set; }         // For OpenFile, DownloadAndRun
        public string Target { get; set; }       // For CreateShortcut
        public string ShortcutName { get; set; } // For CreateShortcut
        public string Url { get; set; }          // For Download
        public string FileName { get; set; }     // For Download
        public string Archive { get; set; }      // For Extract
        public string Destination { get; set; }  // For Extract
    }

    public enum ActionType
    {
        // Hành động đặc biệt cho trình cài đặt chuẩn
        DownloadAndRun,

        // Các hành động đa dụng
        RunCommand,
        CreateShortcut,
        OpenFile,
        RunScript,
        Download,
        Extract
    }
}
