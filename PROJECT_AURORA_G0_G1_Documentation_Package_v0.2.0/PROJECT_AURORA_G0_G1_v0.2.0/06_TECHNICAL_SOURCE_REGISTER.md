# PROJECT AURORA — Technical Source Register

**Review date:** 2026-11-17  
**Source policy:** Microsoft Learn / official .NET documentation preferred.  
**Use:** Baseline technical assumptions; all API behavior must still be validated on target Windows 11 builds.

## 1. .NET lifecycle

1. .NET Support Policy  
   https://dotnet.microsoft.com/en-us/platform/support/policy  
   On 2026-11-17, the official policy lists .NET 10 as LTS and .NET 11 as STS. Both are active; the published end-of-support date is November 14, 2028. .NET 10 remains the proposed baseline because of its LTS designation, but this choice must be revalidated against Windows App SDK compatibility before implementation.

## 2. Windows App SDK

2. Latest Windows App SDK downloads  
   https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads  
   The official downloads page lists Windows App SDK 2.6.5 as Stable, released 2026-11-02.

3. What's new for Windows developers  
   https://learn.microsoft.com/en-us/windows/apps/whats-new/whats-new-for-developers  
   The November 2026 page identifies Windows App SDK 2.6.5 as latest stable and 2.6.6-experimental as experimental. It also notes fixes relevant to multi-adapter/multi-monitor rendering and composition reliability in 2.6.5.

4. Windows App SDK release channels  
   https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels  
   Stable is the production channel; Preview and Experimental are not production-supported channels. Verify the live channel page at implementation start.

## 3. Taskbar / AppBar

5. Application Desktop Toolbars  
   https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars  
   Describes system AppBar services for application-defined appbars, including coordination with other appbars and the taskbar. It does not document third-party shell replacement.

6. SHAppBarMessage function  
   https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shappbarmessage  
   Documents AppBar message operations including `ABM_NEW`, `ABM_REMOVE`, `ABM_QUERYPOS`, `ABM_SETPOS` and taskbar state queries.

## 4. Media and platform integrations

7. GlobalSystemMediaTransportControlsSessionManager  
   https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager  
   Documents access to playback sessions that have integrated with SystemMediaTransportControls. Session availability and command support are player-dependent.

8. Windows Core Audio APIs  
   https://learn.microsoft.com/en-us/windows/win32/coreaudio/about-the-windows-core-audio-apis  
   Background for endpoint and audio-session integration. Detailed endpoint/session implementation choices require G1 validation.

9. Windows notifications overview  
   https://learn.microsoft.com/en-us/windows/apps/develop/notifications/  
   Baseline for Aurora-originated notifications. Does not establish universal access to third-party notification history.

## 5. Evidence limitations

- The AppBar documentation supports app-defined desktop toolbars and system coordination; it does not establish that a third-party application can replace Explorer's native taskbar.
- No universal third-party notification-history capture capability is assumed.
- Wi-Fi, Bluetooth, brightness and virtual-desktop capabilities are not accepted as guaranteed until tested against the precise Windows build, hardware and permission context.
- A documented API's existence does not prove every requested operation is available to a standard-user desktop application on every device.

## 6. Review discipline

At G1 execution approval and again before implementation:
- Re-open each source and verify it remains current.
- Pin exact SDK/runtime package versions.
- Record Windows build numbers and hardware used.
- Capture API capability and failure behavior as evidence.
- Update the TDD and ADRs when source or measured behavior changes.
