namespace Aurora.Core.Primitives;

public enum CapabilityState
{
    DirectlySupported,
    SupportedWithConsent,
    DeepLinkFallback,
    Unavailable,
    PermissionRequired,
    Error
}

public enum ControlOperationalState
{
    Off,
    On,
    Pending,
    Unavailable,
    PermissionRequired,
    Error
}

public sealed record AuroraError(
    string Code,
    string Message,
    string? FallbackDeepLinkUri = null,
    bool IsRecoverable = true);

public sealed record AuroraResult
{
    public bool IsSuccess { get; init; }
    public CapabilityState Capability { get; init; } = CapabilityState.DirectlySupported;
    public AuroraError? Error { get; init; }

    public static AuroraResult Ok(CapabilityState capability = CapabilityState.DirectlySupported) =>
        new() { IsSuccess = true, Capability = capability };

    public static AuroraResult Fail(
        string code,
        string message,
        CapabilityState capability = CapabilityState.Error,
        string? fallbackDeepLinkUri = null,
        bool isRecoverable = true) =>
        new()
        {
            IsSuccess = false,
            Capability = capability,
            Error = new AuroraError(code, message, fallbackDeepLinkUri, isRecoverable)
        };
}

public sealed record AuroraResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public CapabilityState Capability { get; init; } = CapabilityState.DirectlySupported;
    public AuroraError? Error { get; init; }

    public static AuroraResult<T> Ok(T value, CapabilityState capability = CapabilityState.DirectlySupported) =>
        new() { IsSuccess = true, Value = value, Capability = capability };

    public static AuroraResult<T> Fail(
        string code,
        string message,
        CapabilityState capability = CapabilityState.Error,
        string? fallbackDeepLinkUri = null,
        T? fallbackValue = default) =>
        new()
        {
            IsSuccess = false,
            Value = fallbackValue,
            Capability = capability,
            Error = new AuroraError(code, message, fallbackDeepLinkUri)
        };
}

public readonly record struct PhysicalPixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public readonly record struct EffectiveDipRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public PhysicalPixelRect ToPhysicalPixels(double scaleFactor)
    {
        var s = scaleFactor <= 0 ? 1.0 : scaleFactor;
        return new PhysicalPixelRect(
            (int)Math.Round(X * s),
            (int)Math.Round(Y * s),
            Math.Max(1, (int)Math.Round(Width * s)),
            Math.Max(1, (int)Math.Round(Height * s)));
    }

    public static EffectiveDipRect FromPhysicalPixels(PhysicalPixelRect px, double scaleFactor)
    {
        var s = scaleFactor <= 0 ? 1.0 : scaleFactor;
        return new EffectiveDipRect(px.X / s, px.Y / s, px.Width / s, px.Height / s);
    }
}

public sealed record MonitorDescriptor(
    string DeviceId,
    string FriendlyName,
    bool IsPrimary,
    uint EffectiveDpi,
    PhysicalPixelRect BoundsPx,
    PhysicalPixelRect WorkAreaPx)
{
    public double ScaleFactor => EffectiveDpi <= 0 ? 1.0 : EffectiveDpi / 96.0;
    public int ScalePercent => (int)Math.Round(ScaleFactor * 100.0);
    public EffectiveDipRect BoundsDip => EffectiveDipRect.FromPhysicalPixels(BoundsPx, ScaleFactor);
    public EffectiveDipRect WorkAreaDip => EffectiveDipRect.FromPhysicalPixels(WorkAreaPx, ScaleFactor);
}
