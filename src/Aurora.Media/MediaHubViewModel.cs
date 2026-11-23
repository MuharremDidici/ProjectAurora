using Aurora.Core.Contracts;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Presentation;

namespace Aurora.Media;

public sealed class MediaHubViewModel(IMediaSessionService mediaService) : ObservableViewModel
{
    private IReadOnlyList<MediaSessionSnapshot> _sessions = [];
    private MediaSessionSnapshot? _selectedSession;
    private string? _statusBanner;

    public IReadOnlyList<MediaSessionSnapshot> Sessions
    {
        get => _sessions;
        private set => SetProperty(ref _sessions, value);
    }

    public MediaSessionSnapshot? SelectedSession
    {
        get => _selectedSession;
        private set => SetProperty(ref _selectedSession, value);
    }

    public string? StatusBanner
    {
        get => _statusBanner;
        private set => SetProperty(ref _statusBanner, value);
    }

    public bool HasActiveSession => SelectedSession is not null;

    public async Task RefreshSessionsAsync(CancellationToken ct = default)
    {
        var res = await mediaService.GetActiveMediaSessionsAsync(ct);
        if (!res.IsSuccess || res.Value is null || res.Value.Count == 0)
        {
            Sessions = [];
            SelectedSession = null;
            OnPropertyChanged(nameof(HasActiveSession));
            return;
        }

        Sessions = res.Value;
        var prevId = SelectedSession?.SessionId;
        SelectedSession = Sessions.FirstOrDefault(s => s.SessionId == prevId) ?? Sessions[0];
        OnPropertyChanged(nameof(HasActiveSession));
    }

    public void SelectSession(string sessionId)
    {
        var found = Sessions.FirstOrDefault(s => s.SessionId == sessionId);
        if (found is not null)
            SelectedSession = found;
    }

    public async Task<AuroraResult> TogglePlayPauseAsync(CancellationToken ct = default)
    {
        if (SelectedSession is null)
            return AuroraResult.Fail("MEDIA_NO_SESSION", "Aktif medya oturumu yok.");

        var res = await mediaService.ExecuteTransportAsync(SelectedSession.SessionId, "playpause", ct);
        StatusBanner = res.IsSuccess ? null : res.Error?.Message;
        await RefreshSessionsAsync(ct);
        return res;
    }

    public async Task<AuroraResult> NextTrackAsync(CancellationToken ct = default)
    {
        if (SelectedSession is null || !SelectedSession.CanGoNext)
            return AuroraResult.Fail("MEDIA_NEXT_UNSUPPORTED", "Sonraki parça komutu desteklenmiyor.", CapabilityState.Unavailable);

        var res = await mediaService.ExecuteTransportAsync(SelectedSession.SessionId, "next", ct);
        StatusBanner = res.IsSuccess ? null : res.Error?.Message;
        await RefreshSessionsAsync(ct);
        return res;
    }

    public async Task<AuroraResult> PreviousTrackAsync(CancellationToken ct = default)
    {
        if (SelectedSession is null || !SelectedSession.CanGoPrevious)
            return AuroraResult.Fail("MEDIA_PREV_UNSUPPORTED", "Önceki parça komutu desteklenmiyor.", CapabilityState.Unavailable);

        var res = await mediaService.ExecuteTransportAsync(SelectedSession.SessionId, "previous", ct);
        StatusBanner = res.IsSuccess ? null : res.Error?.Message;
        await RefreshSessionsAsync(ct);
        return res;
    }
}
