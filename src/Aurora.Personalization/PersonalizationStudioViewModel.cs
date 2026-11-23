using Aurora.Application.Security;
using Aurora.Application.Services;
using Aurora.Core.Contracts;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;
using Aurora.Presentation;

namespace Aurora.Personalization;

public sealed class PersonalizationStudioViewModel(
    AuroraStateStore stateStore,
    IThemePackageStore packageStore) : ObservableViewModel
{
    private string? _validationBanner;
    private bool _lastOpSucceeded = true;

    public AuroraThemeTokens WorkingTheme => stateStore.EffectiveTheme;
    public AuroraThemeTokens CommittedTheme => stateStore.CommittedTheme;
    public bool IsPreviewActive => stateStore.IsPreviewActive;
    public IReadOnlyList<AuroraThemeTokens> AvailableThemes => stateStore.AllThemes;

    public double PrimaryTextContrastRatio =>
        ContrastCalculator.CalculateContrastRatio(WorkingTheme.Colors.TextPrimary, WorkingTheme.Colors.Surface1);

    public bool MeetsWcagAA =>
        ContrastCalculator.MeetsWcagAA(WorkingTheme.Colors.TextPrimary, WorkingTheme.Colors.Surface1);

    public string? ValidationBanner
    {
        get => _validationBanner;
        private set => SetProperty(ref _validationBanner, value);
    }

    public bool LastOpSucceeded
    {
        get => _lastOpSucceeded;
        private set => SetProperty(ref _lastOpSucceeded, value);
    }

    public void PreviewBuiltInTheme(string themeId)
    {
        var theme = stateStore.ResolveThemeById(themeId);
        stateStore.StartThemePreview(theme);
        ValidationBanner = $"'{theme.DisplayName}' önizleniyor — kalıcı olması için 'Temayı Uygula' butonuna tıklayın.";
        LastOpSucceeded = true;
        RaiseAll();
    }

    public void UpdatePreviewTokens(Func<AuroraThemeTokens, AuroraThemeTokens> mutator)
    {
        var baseTheme = stateStore.EffectiveTheme;
        var baseId = baseTheme.Id.StartsWith("custom-", StringComparison.OrdinalIgnoreCase)
            ? baseTheme.Id
            : $"custom-{baseTheme.Family.ToString().ToLowerInvariant()}";

        var candidate = mutator(baseTheme with
        {
            Id = baseId,
            DisplayName = baseTheme.DisplayName.Contains("(Özel)") ? baseTheme.DisplayName : $"{baseTheme.DisplayName} (Özel)"
        });

        var errors = new List<string>();
        var warnings = new List<string>();
        ThemePackageValidator.ValidateThemeTokens(candidate, errors, warnings);

        if (errors.Count > 0)
        {
            LastOpSucceeded = false;
            ValidationBanner = string.Join(" ", errors);
            return;
        }

        stateStore.StartThemePreview(candidate);
        LastOpSucceeded = true;
        ValidationBanner = warnings.Count > 0
            ? $"Önizleme aktif (Uyarı: {string.Join(" ", warnings)})"
            : "Canlı önizleme aktif — değişiklikler onaylanmayı bekliyor.";
        RaiseAll();
    }

    public async Task ApplyCurrentPreviewAsync(CancellationToken ct = default)
    {
        var target = stateStore.EffectiveTheme;
        await stateStore.ApplyThemeAsync(target, ct);
        LastOpSucceeded = true;
        ValidationBanner = $"'{target.DisplayName}' teması başarıyla uygulandı ve kaydedildi.";
        RaiseAll();
    }

    public void DiscardPreview()
    {
        stateStore.DiscardThemePreview();
        LastOpSucceeded = true;
        ValidationBanner = "Önizleme iptal edildi, kayıtlı temaya dönüldü.";
        RaiseAll();
    }

    public async Task ResetToDefaultAsync(CancellationToken ct = default)
    {
        await stateStore.ResetThemeToDefaultAsync(ct);
        LastOpSucceeded = true;
        ValidationBanner = "Varsayılan Aurora Glass kurtarma temasına sıfırlandı.";
        RaiseAll();
    }

    public async Task<AuroraResult> ImportThemePackageForPreviewAsync(string filePath, CancellationToken ct = default)
    {
        var res = await packageStore.ImportPackageAsync(filePath, ct);
        if (!res.IsSuccess || res.Value is null)
        {
            LastOpSucceeded = false;
            ValidationBanner = res.Error?.Message ?? "Tema paketi doğrulanamadı.";
            return AuroraResult.Fail(res.Error?.Code ?? "IMPORT_ERR", ValidationBanner);
        }

        // Sandboxed preview first per Design System §9!
        stateStore.StartThemePreview(res.Value.Theme);
        LastOpSucceeded = true;
        ValidationBanner = $"'{res.Value.Name}' paketi güvenlik taramasından geçti ve Korumalı Önizlemeye yüklendi. Uygulamak için onaylayın.";
        RaiseAll();
        return AuroraResult.Ok();
    }

    public async Task<AuroraResult<string>> ExportCurrentThemeAsync(string destinationPath, CancellationToken ct = default)
    {
        var res = await packageStore.ExportPackageAsync(WorkingTheme, destinationPath, "Project Aurora Studio", ct);
        LastOpSucceeded = res.IsSuccess;
        ValidationBanner = res.IsSuccess
            ? $"Tema paketi dışa aktarıldı: {Path.GetFileName(destinationPath)}"
            : res.Error?.Message;
        return res;
    }

    public void RaiseAll()
    {
        OnPropertyChanged(nameof(WorkingTheme));
        OnPropertyChanged(nameof(CommittedTheme));
        OnPropertyChanged(nameof(IsPreviewActive));
        OnPropertyChanged(nameof(AvailableThemes));
        OnPropertyChanged(nameof(PrimaryTextContrastRatio));
        OnPropertyChanged(nameof(MeetsWcagAA));
    }
}
