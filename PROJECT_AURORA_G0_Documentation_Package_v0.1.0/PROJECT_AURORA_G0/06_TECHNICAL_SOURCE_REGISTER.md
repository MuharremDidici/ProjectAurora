# PROJECT AURORA — Technical Source Register

Sources consulted on 2026-11-17. API feasibility statements in the TDD are preliminary; verify exact API availability and behavior in G1 on the target Windows 11 builds.

1. .NET official support policy  
   https://dotnet.microsoft.com/en-us/platform/support/policy  
   Used for .NET 10 LTS vs .NET 11 STS lifecycle decision.

2. Windows App SDK — What's new for Windows developers  
   https://learn.microsoft.com/en-us/windows/apps/whats-new/whats-new-for-developers  
   Used for current SDK release information as of November 2026.

3. Windows App SDK release channels  
   https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels  
   Used to distinguish Stable, Preview and Experimental channels.

4. Windows App SDK downloads  
   https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads  
   Used to verify current stable release listing.

5. Windowing overview for WinUI and Windows App SDK  
   https://learn.microsoft.com/en-us/windows/apps/develop/ui/windowing-overview  
   Used for Window, AppWindow and HWND model.

6. Application Desktop Toolbars  
   https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars  
   Used for documented AppBar coordination model.

7. SHAppBarMessage function  
   https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shappbarmessage  
   Used for AppBar message contract.

8. Windows notifications overview  
   https://learn.microsoft.com/en-us/windows/apps/develop/notifications/  
   Used for selecting AppNotificationManager for new Windows App SDK apps.

9. About the Windows Core Audio APIs  
   https://learn.microsoft.com/en-us/windows/win32/coreaudio/about-the-windows-core-audio-apis  
   Used for endpoint and audio-session API family.

10. EndpointVolume API  
    https://learn.microsoft.com/en-us/windows/win32/coreaudio/endpointvolume-api  
    Used for master endpoint volume behavior.

Important limitation: this source register does not claim that Microsoft documents a general third-party API to replace Explorer's taskbar or universally retrieve third-party notification history. Those capabilities remain unproven and are explicitly excluded from the baseline promise.
