using Aurora.Application.Services;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Presentation;

namespace Aurora.Workspaces;

public sealed class WorkspaceManagerViewModel(AuroraStateStore stateStore) : ObservableViewModel
{
    private string? _statusMessage;

    public IReadOnlyList<WorkspaceProfile> Profiles => stateStore.Profiles;
    public WorkspaceProfile ActiveProfile => stateStore.ActiveProfile;

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public async Task<AuroraResult<WorkspaceProfile>> ActivateProfileAsync(string profileId, CancellationToken ct = default)
    {
        var res = await stateStore.SwitchProfileAsync(profileId, ct);
        if (res.IsSuccess && res.Value is not null)
        {
            StatusMessage = $"'{res.Value.Name}' profili etkinleştirildi.";
            await stateStore.AddNotificationAsync(
                sourceModule: "Çalışma Alanları",
                category: NotificationCategory.Workspace,
                severity: NotificationSeverity.Success,
                title: $"Profil Değiştirildi: {res.Value.Name}",
                body: res.Value.Description,
                showToast: true,
                ct: ct);
        }
        else
        {
            StatusMessage = res.Error?.Message;
        }

        OnPropertyChanged(nameof(Profiles));
        OnPropertyChanged(nameof(ActiveProfile));
        return res;
    }

    public void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(Profiles));
        OnPropertyChanged(nameof(ActiveProfile));
    }
}
