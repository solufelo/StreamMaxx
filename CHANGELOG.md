# Changelog

All notable changes to **StreamMaxx** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-09-15

### Added
- **Native Desktop Application**: Standalone 32KB Win32 executable (`StreamMaxx.exe`) with custom dark gamer theme, PerMonitorV2 High-DPI scaling, and system tray integration.
- **Zero-Copy Video Multiplexing**: Go-based MediaMTX relay engine routing single NVENC master streams simultaneously to Twitch, YouTube, Kick, and TikTok Live.
- **Hardware Thread Isolation**: Win32 API affinity pinning (`SetProcessAffinityMask(0xF000)`) allocating broadcast threads to Ryzen Cores 6?7 (Logical 12?15) and reserving Cores 0?5 for game engines.
- **Network QoS Hardening**: Windows `NetQosPolicy` tagging outbound RTMP/SRT packets with **DSCP 46 (Expedited Forwarding)** to prevent in-game bufferbloat.
- **Hardware Telemetry HUD**: Live asynchronous `nvidia-smi` telemetry reporting NVENC load %, GPU temperature, VRAM usage, and stream health.
- **Turnkey 1-Click Bootstrap**: Automated `install.bat` compilation and environment setup script.
- **GitHub Actions CI**: Automated build and credential security audit pipeline.
