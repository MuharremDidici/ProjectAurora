using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Aurora.Platform.Windows.ShellExtras;

/// <summary>
/// Activates clicked Windows 11 notifications just like native Action Center (zero UAC):
/// 1) Protocol URI deep-links (ms-screensketch:, antigravity-ide:, whatsapp:, msteams:, https:, etc.)
/// 2) COM IApplicationActivationManager.ActivateApplication(AUMID, launchArgs) for Store/MSIX apps (WhatsApp, Teams, Mail)
///    so the app opens AND jumps directly to the exact chat/conversation!
/// 3) Clean fallback to focusing an already-open Win32 window or shell:AppsFolder launch.
/// </summary>
public static partial class NotificationActivationService
{
    private static readonly Guid ClsidApplicationActivationManager = new("45BA127D-10A8-46EA-8AB7-56EA9078943C");

    [ComImport]
    [Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
            uint options,
            out uint processId);
    }

    public static bool TryExtractLaunchUri(string? launchArgs, string? activationType, out string uri)
    {
        uri = string.Empty;
        string raw = (launchArgs ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        bool explicitProtocol = string.Equals(activationType, "protocol", StringComparison.OrdinalIgnoreCase);
        if (explicitProtocol && UriSchemeRegex().IsMatch(raw))
        {
            uri = raw;
            return true;
        }

        if (SafeDeepLinkPrefixRegex().IsMatch(raw))
        {
            uri = raw;
            return true;
        }

        return false;
    }

    public static bool TryActivateViaProtocolUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Invokes Windows' native IApplicationActivationManager with the exact AUMID and toast launch payload.
    /// This passes the deep-link arguments (e.g. WhatsApp chat JID / Teams thread) straight into the app!
    /// </summary>
    public static bool TryActivateViaAumid(string? primaryId, string? launchArgs)
    {
        string aumid = (primaryId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(aumid))
            return false;

        try
        {
            var type = Type.GetTypeFromCLSID(ClsidApplicationActivationManager);
            if (type is null)
                return false;

            var obj = Activator.CreateInstance(type);
            if (obj is not IApplicationActivationManager mgr)
                return false;

            try
            {
                int hr = mgr.ActivateApplication(aumid, string.IsNullOrWhiteSpace(launchArgs) ? null : launchArgs, 0u, out _);
                return hr == 0;
            }
            finally
            {
                Marshal.ReleaseComObject(mgr);
            }
        }
        catch
        {
            return false;
        }
    }

    public static bool TryLaunchViaAppsFolder(string? primaryId)
    {
        string aumid = (primaryId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(aumid))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{aumid}",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+\-.]{1,30}:", RegexOptions.CultureInvariant)]
    private static partial Regex UriSchemeRegex();

    [GeneratedRegex(@"^(https?|whatsapp|msteams|ms-screensketch|antigravity-ide|vscode|discord|slack|tg|mailto|ms-settings|outlookmail):", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SafeDeepLinkPrefixRegex();
}
