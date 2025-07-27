# ProfMix: Professional Voice Over Mixer

<div align="center">
  <img src="https://media.buckets.dadoftheclan.com/07272025_ProfMix_Logo_Transparent.png" alt="ProfMix Logo" width="200" height="200">
  
  **A Windows application for seamless voice and music mixing**
  
  [![License: WTFPL](https://img.shields.io/badge/License-WTFPL-brightgreen.svg)](http://www.wtfpl.net/about/)
  [![Platform](https://img.shields.io/badge/platform-Windows-blue.svg)]()
  [![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2+-purple.svg)]()
</div>

---

## 📖 Overview

ProfMix is a Windows Presentation Foundation (WPF) application designed to simplify the process of mixing voice recordings with background music. It's ideal for creating high-quality audio content for various applications, including **IVR (Interactive Voice Response) systems**, **podcasts**, **presentations**, and more.

The application provides a user-friendly interface for selecting audio files, adjusting mixing parameters like volume and music offset, managing audio output profiles, and previewing results. It leverages the powerful **NAudio** library for robust audio processing.

## ✨ Features

### 🎵 Audio Mixing
- **Voice & Music Mixing**: Combine a primary voice recording with a background music track
- **Flexible Audio Input**: Supports common audio formats like WAV and MP3 for both voice and music
- **Volume Control**: Independently adjust the volume levels for voice and music tracks
- **Music Offset & Fade-Out**: Set a start delay for background music and define a smooth fade-out duration for professional endings

### 🎛️ Audio Profiles
- **Custom Profiles**: Create, save, and manage custom audio output profiles (sample rate, bit depth, channels, max file size)
- **PBX Templates**: Includes pre-defined templates for popular PBX systems:
  - 3CX
  - FreePBX
  - Sangoma
  - Genesys
  - 8x8
- **Import/Export**: Share or backup your custom profiles easily

### 🔧 Advanced Features
- **Audio Preview**: Listen to individual voice, music, and the final mixed audio directly within the application
- **File Size Management**: Automatic estimation of output file size with warnings for common system limits (e.g., 3CX's 10MB limit)
- **Robust Error Handling**: Comprehensive logging and a dedicated exception window for critical errors
- **Intuitive User Interface**: Designed for ease of use with clear controls and real-time feedback

## 🚀 Getting Started

### Prerequisites

- **Windows Operating System**: ProfMix is a WPF application designed for Windows
- **.NET Framework**: Requires .NET Framework 4.7.2 or later

### Installation

1. **Clone the Repository**
   ```bash
   git clone https://github.com/your-username/ProfMix.git
   cd ProfMix
   ```

2. **Open in Visual Studio**
   - Open the `ProfMix.sln` solution file in Visual Studio (2019 or later recommended)

3. **Restore NuGet Packages**
   - Visual Studio should automatically restore the necessary NuGet packages (NAudio, Newtonsoft.Json)
   - If not, right-click on the solution in Solution Explorer and select "Restore NuGet Packages"

4. **Build the Project**
   - Build the solution: `Build > Build Solution` or `Ctrl+Shift+B`

5. **Run the Application**
   - Run from Visual Studio: `Debug > Start Debugging` or `F5`
   - Or navigate to `bin/Debug` (or `bin/Release`) folder and run `App_AudioVoice.exe`

## 💡 Usage Guide

### Step 1: Select Your Audio Files
- **Voice Recording**: Click "Browse" to select your primary voice audio file (WAV or MP3)
- **Background Music**: Click "Browse" to choose your background music track (WAV or MP3)
- **Output Location**: Click "Save As" to specify where your final mixed audio file will be saved

### Step 2: Adjust Mixing Settings
| Setting | Range | Description |
|---------|-------|-------------|
| **Voice Volume** | 0% - 100% | Control the loudness of your voice recording |
| **Music Volume** | 0% - 100% | Set the intensity of background music (20-40% recommended) |
| **Music Offset** | 0 - 120 seconds | Delay the start of background music (useful for intros) |
| **Buffer Length** | 0 - 30 seconds | Duration of fade-out applied to music after voice track ends |

### Step 3: Manage Audio Profiles
- **Select Profile**: Choose an existing audio profile from the dropdown list
- **Manage Profiles**: Click "Manage Profiles" to:
  - Create new custom profiles
  - Apply pre-configured PBX system templates
  - Edit or delete existing profiles
  - Import/export profiles for backup and sharing

### Step 4: Preview & Mix
1. Use **Preview** buttons to listen to your audio files before finalizing
2. Click **"Mix Audio Files"** to start the mixing process
3. Monitor progress and receive completion notifications with file details

## 📂 Project Architecture

### Key Components

| File | Purpose |
|------|---------|
| `App.xaml.cs` | Application entry point and global exception handling |
| `Windows/wndMix.xaml.cs` | Main mixing window logic and UI interaction |
| `Windows/wndProfile.xaml.cs` | Audio profile management interface |
| `Windows/wndHelp.xaml.cs` | In-app help documentation |
| `Windows/wndException.xaml.cs` | Critical error display window |
| `Windows/wndNotification.xaml.cs` | Custom toast notification system |

### Core Systems

| File | Functionality |
|------|---------------|
| `Systems/sysInteract.cs` | UI interaction utilities and dialogs |
| `Systems/sysState.cs` | Application state management and logging |
| `Systems/sysMixer.cs` | Core audio mixing logic using NAudio |
| `Models/mdlProfile.cs` | Audio profile data model and validation |
| `Providers/proLooping.cs` | Custom NAudio provider for audio looping |
| `Providers/proFadeOut.cs` | Custom NAudio provider for fade-out effects |

## 🐛 Error Handling & Logging

ProfMix includes comprehensive error handling:

- **UI Thread Exceptions**: Unhandled exceptions on the UI thread are caught and reported
- **Domain Exceptions**: General application errors from any thread are handled
- **Async Task Exceptions**: Exceptions from unobserved asynchronous tasks are caught

### Log Files Location
```
%APPDATA%\AudioMixerPro\Profiles\
├── application.log
└── crash.log
```

### Error Recovery Options
When a critical error occurs, the dedicated exception window allows you to:
- 📋 Copy error details to clipboard
- 📝 Open crash.log file in Notepad
- 🔄 Restart the application
- ❌ Exit the application

## 🤝 Contributing

We welcome contributions! Here's how you can help:

1. **Fork** the repository
2. **Create** a new branch: `git checkout -b feature/YourFeature`
3. **Make** your changes
4. **Commit** your changes: `git commit -m 'Add new feature'`
5. **Push** to the branch: `git push origin feature/YourFeature`
6. **Open** a Pull Request

### Areas for contribution:
- 🐛 Bug fixes and stability improvements
- ✨ New audio processing features
- 🎨 UI/UX enhancements
- 📚 Documentation improvements
- 🧪 Unit tests and quality assurance

## 📄 License

This project is licensed under the **Do What The Fuck You Want To Public License (WTFPL)**.

```
DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE
Version 2, December 2004

Copyright (C) 2004 Sam Hocevar <sam@hocevar.net>

Everyone is permitted to copy and distribute verbatim or modified
copies of this license document, and changing it is allowed as long
as the name is changed.

DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE
TERMS AND CONDITIONS FOR COPYING, DISTRIBUTION AND MODIFICATION

0. You just DO WHAT THE FUCK YOU WANT TO.
```

---

<div align="center">
  <strong>Made with ❤️ for audio professionals and enthusiasts</strong>
  
  [Report Bug](../../issues) • [Request Feature](../../issues) • [Documentation](../../wiki)
</div>
