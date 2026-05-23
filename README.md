> [!WARNING]
> The Python version of this project (`discordrpc.py` and related files) is **outdated and deprecated**. Please use the new C# (.NET 8) implementation found in this repository. The Python files are kept here for reference only and should not be used.

# WindowRPC
WindowRPC is a C# (.NET 8) Windows application that automatically updates your Discord status with your currently focused window as Rich Presence. It offers flexible options such as custom statuses per application, allowing you to tailor your Discord presence to your preferences.

## Supported Platforms
Windows (this branch)

[Linux (KDE Plasma Wayland)](https://github.com/ios7jbpro/WindowRPC/tree/kde-linux)

## Features
- Automatically updates Discord Rich Presence based on the currently active window.
- Customize your status for specific applications via `overrides.json`.
- System tray integration for easy access and control.

## Requirements
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or SDK for building)
- Windows 10 (version 2004) or later

## Installation

### Clone the Repository
```bash
git clone https://github.com/yourusername/WindowRPC.git
cd WindowRPC
```

### Build the Project
```bash
dotnet build --configuration Release
```

Or publish a standalone executable:
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### Create a Discord Application
Go to the [Discord Developer Portal](https://discord.com/developers/applications) and create a new application. Note the **Application ID** - this is what will be shown in the rich presence.

### Run the Application
```bash
dotnet run
```

Or run the compiled executable from the `bin/Release` folder.

## Usage
Once running, WindowRPC will sit in your system tray and automatically update your Discord status based on the active window. You can customize specific application statuses by editing the `overrides.json` file.

## Customization
[Guide](https://github.com/ios7jbpro/WindowRPC/tree/overrides-guide)

## Contributors
- ios7jbpro
- kurtbahartr
- ChatGPT (THIS PROJECT IS HEAVILY SLOPCODED I DON'T CARE IF YOU LIKE IT OR NOT)
- This project was intended to be AI-generated only. However, simple contributions that remain easy for AI to parse and understand are welcome. Please submit issues or pull requests that align with this guideline.
