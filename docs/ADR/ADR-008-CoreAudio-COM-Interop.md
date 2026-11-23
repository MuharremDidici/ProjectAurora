# ADR-008: Audio Implementation & Native COM Interop Strategy

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** Windows Platform Lead

## Context
Aurora Control Center requires real master endpoint volume/mute (`CC-004`), active audio output/input endpoint discovery (`CC-005`), and per-application audio session volume/mute (`CC-006`) without relying on brittle external binaries or unmaintained third-party NuGet wrappers (`R-011`, `R-018`).

## Decision
Implement direct, zero-dependency, documented **Windows Core Audio COM interop** inside `Aurora.Platform.Windows`:
- `IMMDeviceEnumerator` (`CLSID_MMDeviceEnumerator`) -> `EnumAudioEndpoints` & `GetDefaultAudioEndpoint` for Render and Capture devices, reading `PKEY_Device_FriendlyName` via `IPropertyStore`.
- `IAudioEndpointVolume` for exact scalar `[0.0 .. 1.0]` master volume read/write and mute toggle.
- `IAudioSessionManager2` -> `IAudioSessionEnumerator` -> `IAudioSessionControl2` + `ISimpleAudioVolume` for enumerating active per-process audio sessions, resolving process names/icons safely, and setting session-specific volume and mute.
- Default endpoint switching: Windows does not publish a supported public Win32 API for changing default audio endpoints (`IPolicyConfig` is undocumented); per `DEC-004`/`02_AURORA_TDD.md` §1.3 ("Use documented and supported APIs wherever possible; never fake success"), Aurora lists all active endpoints accurately, indicates the current Windows default endpoint, and routes default-device switching honestly to `ms-settings:sound` (`mmsys.cpl`) while controlling volume/mute directly via `IAudioEndpointVolume`.
