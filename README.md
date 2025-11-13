# HieuckIT App Installer

A straightforward Windows utility designed to streamline the installation of your favorite applications. Configure once with a simple YAML file, and let the installer handle the download and silent installation process for you.

## Features

- **YAML-Driven:** Easily define the applications you want to install using a clean, human-readable `apps.yaml` file.
- **Automated Installation:** The application automatically downloads installers and runs them with silent arguments for a hands-off setup.
- **Patch Support:** Includes functionality to apply patches or additional configuration files after installation.
- **Flexible Configuration:** The app can use a local `apps.yaml` file or fetch the latest version from an online repository.
- **Portable:** No installation needed for the installer itself. Just run the executable.
- **Architecture Aware:** Automatically selects the correct installer version (x86 or x64) for your system.
- **CI/CD Ready:** Includes a GitHub Actions workflow to automatically build and release the application.

## How It Works

1.  **Load Configuration:** On launch, the installer first looks for an `apps.yaml` file in its directory.
2.  **Fallback to Online:** If a local configuration is not found, it attempts to download the latest `apps.yaml` from a predefined online URL.
3.  **Display Apps:** The application list from the YAML file is parsed and displayed in the user interface.
4.  **Install:** The user selects the desired applications and clicks "Install". The utility then downloads the necessary files and executes the installation and patching processes silently in the background.

## Usage

1.  Download the latest release from the [GitHub Actions tab](https://github.com/hieuck/HieuckIT-App-Installer/actions).
2.  Unzip the `HieuckIT-App-Installer-Release.zip` file.
3.  Run `HieuckIT-App-Installer.exe`.
4.  Select the applications you wish to install from the list.
5.  Click the **Install** button and wait for the process to complete.

## Configuration (`apps.yaml`)

The application's behavior is controlled by the `apps.yaml` file, which contains a list of applications. Each application is an object with the following properties:

```yaml
applications:
  - Name: "Example App"
    ProcessName: "example.exe"
    RegistryDisplayName: "Example App"
    InstallerArgs: "/S"
    DownloadLinks:
      - Name: "Installer"
        Url_x64: "https://example.com/installer_64.exe"
        Url_x86: "https://example.com/installer_32.exe"
    IsArchive: false
    PatchArgs: 'x -y "{patch_path}" -o"{install_dir}"'
    PatchLinks:
      - Name: "Patch"
        Url_x64: "https://example.com/patch_64.rar"
        Url_x86: "https://example.com/patch_32.rar"

```
- **Name:** The display name of the application in the UI.
- **ProcessName:** The executable process name to check if the application is running or to create shortcuts.
- **RegistryDisplayName:** The name found in the Windows Registry to verify if the application is already installed.
- **InstallerArgs:** The command-line arguments for silent installation (e.g., `/S`, `/VERYSILENT`).
- **DownloadLinks:** A list containing download links for the installer. `Url_x64` and `Url_x86` specify the links for 64-bit and 32-bit systems, respectively.
- **IsArchive:** Set to `true` if the downloaded file is a zip/rar archive that needs extraction instead of a standard installer.
- **PatchArgs:** Command-line arguments for the patching tool (7-Zip) to apply patches.
- **PatchLinks:** A list of download links for patch files.

---

## Ủng hộ tôi (Support Me)

- **Vietcombank:** `9966595263`
- **MoMo:** `0966595263`
- **Chủ tài khoản:** LE TRUNG HIEU
- **PayPal:** [https://www.paypal.me/hieuck](https://www.paypal.me/hieuck)
