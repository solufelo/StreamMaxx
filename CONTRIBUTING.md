# Contributing to StreamMaxx

Thank you for your interest in contributing to **StreamMaxx**! We welcome pull requests for performance enhancements, codec support, UI improvements, and bug fixes.

---

## Development Setup

1. **Prerequisites**:
   - Windows 10 or 11 (64-bit).
   - .NET Framework 4.8 runtime / `csc.exe` (included with Windows).
   - OBS Studio 30+ installed.

2. **Building the Application**:
   Compile `StreamMaxx.exe` directly via command line:
   ```bat
   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ /win32manifest:src\app.manifest /win32icon:src\app.ico /reference:System.dll,System.Windows.Forms.dll,System.Drawing.dll /out:StreamMaxx.exe src\StreamMaxxApp.cs
   ```

3. **Coding Standards**:
   - **Performance First**: Maintain low CPU/GPU footprints (<0.05% CPU, 0% secondary NVENC).
   - **Compatibility**: Ensure all C# code is compatible with C# 5.0 / .NET 4.8 so any Windows user can build it without installing multi-gigabyte external SDKs.
   - **Security**: NEVER hardcode or commit stream keys, session IDs, or personal tokens.

---

## Submitting Pull Requests

1. Fork the repository and create your feature branch:
   ```bash
   git checkout -b feature/ultra-low-latency-tweak
   ```
2. Commit your changes with conventional commit messages:
   ```bash
   git commit -m "perf: reduce thread polling delay to 500ms"
   ```
3. Push to your branch and open a Pull Request against `main`.
