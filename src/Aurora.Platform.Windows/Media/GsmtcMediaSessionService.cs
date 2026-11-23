using Aurora.Core.Contracts;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Windows.Media.Control;

namespace Aurora.Platform.Windows.Media;

public sealed class GsmtcMediaSessionService(IAuroraLogger logger) : IMediaSessionService
{
    public async Task<AuroraResult<IReadOnlyList<MediaSessionSnapshot>>> GetActiveMediaSessionsAsync(CancellationToken ct = default)
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (manager is null)
                return AuroraResult<IReadOnlyList<MediaSessionSnapshot>>.Ok([], CapabilityState.Unavailable);

            var sessions = manager.GetSessions();
            var current = manager.GetCurrentSession();
            var snapshots = new List<MediaSessionSnapshot>();

            int index = 0;
            foreach (var s in sessions)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var props = await s.TryGetMediaPropertiesAsync();
                    var pb = s.GetPlaybackInfo();
                    var ctrls = pb?.Controls;

                    byte[]? artwork = null;
                    if (props?.Thumbnail is not null)
                    {
                        try
                        {
                            using var ras = await props.Thumbnail.OpenReadAsync();
                            if (ras.Size > 0 && ras.Size <= 2 * 1024 * 1024)
                            {
                                using var netStream = ras.AsStreamForRead();
                                using var ms = new MemoryStream();
                                await netStream.CopyToAsync(ms, ct);
                                artwork = ms.ToArray();
                            }
                        }
                        catch
                        {
                            // Optional artwork may not be readable
                        }
                    }

                    bool isCurrent = current is not null &&
                                     string.Equals(current.SourceAppUserModelId, s.SourceAppUserModelId, StringComparison.OrdinalIgnoreCase);

                    var appId = FormatAppSourceName(s.SourceAppUserModelId);
                    var snapshot = new MediaSessionSnapshot(
                        SessionId: $"{index}:{s.SourceAppUserModelId}",
                        SourceAppId: appId,
                        Title: string.IsNullOrWhiteSpace(props?.Title) ? "Medya Oturumu" : props.Title,
                        Artist: props?.Artist ?? string.Empty,
                        AlbumTitle: props?.AlbumTitle ?? string.Empty,
                        IsPlaying: pb?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                        CanPlay: ctrls?.IsPlayEnabled == true || ctrls?.IsPlayPauseToggleEnabled == true,
                        CanPause: ctrls?.IsPauseEnabled == true || ctrls?.IsPlayPauseToggleEnabled == true,
                        CanGoNext: ctrls?.IsNextEnabled == true,
                        CanGoPrevious: ctrls?.IsPreviousEnabled == true,
                        ArtworkBytes: artwork);

                    if (isCurrent)
                        snapshots.Insert(0, snapshot);
                    else
                        snapshots.Add(snapshot);

                    index++;
                }
                catch
                {
                    // Individual session invalidated mid-enumeration
                }
            }

            return AuroraResult<IReadOnlyList<MediaSessionSnapshot>>.Ok(snapshots);
        }
        catch (Exception ex)
        {
            logger.Warn("Media", "GSMTC_QUERY_WARN", $"GSMTC session query failed ({ex.GetType().Name}).");
            return AuroraResult<IReadOnlyList<MediaSessionSnapshot>>.Fail("GSMTC_UNAVAILABLE", "Medya oturum yöneticisine erişilemedi.", CapabilityState.Unavailable);
        }
    }

    public async Task<AuroraResult> ExecuteTransportAsync(string sessionId, string command, CancellationToken ct = default)
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (manager is null)
                return AuroraResult.Fail("GSMTC_NONE", "Medya oturumu bulunamadı.", CapabilityState.Unavailable);

            var sessions = manager.GetSessions();
            GlobalSystemMediaTransportControlsSession? target = null;

            int idx = 0;
            foreach (var s in sessions)
            {
                var key = $"{idx}:{s.SourceAppUserModelId}";
                if (string.Equals(key, sessionId, StringComparison.OrdinalIgnoreCase) ||
                    sessionId == "current")
                {
                    target = s;
                    break;
                }
                idx++;
            }

            target ??= manager.GetCurrentSession();
            if (target is null)
                return AuroraResult.Fail("GSMTC_SESSION_GONE", "Aktif medya oynatıcı kapandı.");

            bool ok = command.ToLowerInvariant() switch
            {
                "playpause" => await target.TryTogglePlayPauseAsync(),
                "play" => await target.TryPlayAsync(),
                "pause" => await target.TryPauseAsync(),
                "next" => await target.TrySkipNextAsync(),
                "previous" => await target.TrySkipPreviousAsync(),
                _ => false
            };

            return ok
                ? AuroraResult.Ok()
                : AuroraResult.Fail("GSMTC_CMD_REJECTED", "Oynatıcı bu medya komutunu desteklemiyor.", CapabilityState.Unavailable);
        }
        catch (Exception ex)
        {
            logger.Warn("Media", "GSMTC_CMD_WARN", $"Media transport command failed ({ex.GetType().Name}).");
            return AuroraResult.Fail("GSMTC_CMD_ERR", "Medya komutu yürütülemedi.");
        }
    }

    private static string FormatAppSourceName(string? aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid))
            return "Medya Oynatıcı";
        if (aumid.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
            return "Spotify";
        if (aumid.Contains("OperaGX", StringComparison.OrdinalIgnoreCase))
            return "Opera GX";
        if (aumid.Contains("Opera", StringComparison.OrdinalIgnoreCase))
            return "Opera";
        if (aumid.Contains("Brave", StringComparison.OrdinalIgnoreCase))
            return "Brave";
        if (aumid.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            return "Firefox";
        if (aumid.Contains("msedge", StringComparison.OrdinalIgnoreCase))
            return "Microsoft Edge";
        if (aumid.Contains("chrome", StringComparison.OrdinalIgnoreCase))
            return "Google Chrome";
        if (aumid.Contains("ZuneMusic", StringComparison.OrdinalIgnoreCase) || aumid.Contains("Music", StringComparison.OrdinalIgnoreCase))
            return "Windows Medya Oynatıcı";

        var clean = Path.GetFileNameWithoutExtension(aumid);
        return string.IsNullOrWhiteSpace(clean) ? aumid : clean;
    }
}
