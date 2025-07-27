# ProfMix: Professional Voice Over Mixer

<div align="center">
  <img src="https://media.buckets.dadoftheclan.com/07272025_ProfMix_Icon_Transparent.png" alt="ProfMix Logo" width="200" height="200">
  
  **A Windows application for seamless voice and music mixing**
  
  [![License: Dad's License](https://img.shields.io/badge/License-Dad's%20License-brightgreen.svg)]()
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

This project is licensed under **Dad's License v1.1** - *build on and forward, but remember your roots.*

```
DAD'S LICENSE
Version 1.1, July 2025
(Based on WTFPL - Do What The Fuck You Want To Public License)
SPDX-License-Identifier: DADSL-1.1
Copyright (C) 2025 DadOfTheClan

"Build on and forward, but remember your roots."

DAD'S LICENSE - TERMS AND CONDITIONS

0. You just DO WHAT THE FUCK YOU WANT TO.

1. If you redistribute this software or substantial portions of it,
   you must include "Originally created by DadOfTheClan" in some
   form of attribution. This attribution may be placed anywhere
   within your distribution - in source code comments, documentation,
   binary metadata, compiled artifacts, or any other location of
   your choosing. The attribution need not be visible to end users
   and may be as inconspicuous as technically feasible while still
   being present and discoverable within the distributed materials.

2. If you modify this license text itself, you must change the name
   from "DAD'S LICENSE" to something else. If you modify the licensed
   software substantially, you should change its name to reflect that
   it's grown into something new while keeping its roots. Think "Taco
   Zone" becoming "Zen Tacos" - same spirit, new identity. Make it
   your own, but remember where it came from - that's the whole point.

3. Build on and forward, but remember your roots.

LEGAL CLARIFICATIONS:

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject only to the attribution and license naming
requirements above.

For the avoidance of doubt, the attribution requirement in Section 1 is
satisfied by any inclusion of the specified text anywhere within the
distributed software package, regardless of visibility, accessibility,
or prominence. No particular placement, formatting, or user visibility
is required.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

This license shall be governed by and construed in accordance with the laws
of the jurisdiction where the copyright holder resides, without regard to
conflict of law principles. Any legal action arising from this license shall
be subject to the exclusive jurisdiction of the courts in said jurisdiction.

USAGE EXAMPLES:

For package.json:
  "license": "DADSL-1.1"

For README badges/descriptions:
  Licensed under DAD'S LICENSE v1.1 – a permissive license that lets you
  do whatever the fuck you want, as long as you remember your roots.

For copyright notices:
  Copyright (C) [YEAR] [NAME]
  Licensed under DAD'S LICENSE v1.1
```

---

<div align="center">
  <strong>Made with ❤️ for audio professionals and enthusiasts</strong>
  
  [Report Bug](../../issues) • [Request Feature](../../issues) • [Documentation](../../wiki)
</div>
