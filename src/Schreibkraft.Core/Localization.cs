using System.Globalization;

namespace Schreibkraft.Core;

/// <summary>UI language selection. Auto = detect from system culture.</summary>
public enum UiLanguage
{
    Auto = 0,
    English,
    German
}

/// <summary>
/// Code-internal default for all UI/prompt strings is English (key system).
/// German is a translation. Access via the static <see cref="L"/> facade.
/// </summary>
public sealed class LocalizationService
{
    private UiLanguage _effective = UiLanguage.English;

    public UiLanguage Setting { get; private set; } = UiLanguage.Auto;
    public UiLanguage Effective => _effective;

    /// <summary>Applies the user's setting; "Auto" resolves to the system UI culture.</summary>
    public void Apply(UiLanguage setting)
    {
        Setting = setting;
        _effective = setting == UiLanguage.Auto ? DetectSystem() : setting;
        var culture = _effective == UiLanguage.German
            ? new CultureInfo("de-DE")
            : new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static UiLanguage DetectSystem()
    {
        var two = CultureInfo.CurrentUICulture?.TwoLetterISOLanguageName?.ToLowerInvariant();
        return two == "de" ? UiLanguage.German : UiLanguage.English;
    }

    public string Get(string key)
    {
        if (_effective == UiLanguage.German && LocalizationStrings.De.TryGetValue(key, out var de))
        {
            return de;
        }
        return LocalizationStrings.En.TryGetValue(key, out var en) ? en : key;
    }

    public string Format(string key, params object?[] args)
    {
        var raw = Get(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, raw, args);
        }
        catch
        {
            return raw;
        }
    }
}

/// <summary>
/// Static facade for compact access (e.g. <c>L.S("nav.overview")</c>).
/// Initialized by the application host via <see cref="Init"/>.
/// </summary>
public static class L
{
    private static LocalizationService _service = new();

    public static LocalizationService Service => _service;
    public static UiLanguage Effective => _service.Effective;
    public static UiLanguage Setting => _service.Setting;

    /// <summary>Replaces the underlying service (used by tests/host bootstrap).</summary>
    public static void Init(LocalizationService service) => _service = service;

    public static void Apply(UiLanguage setting) => _service.Apply(setting);

    public static string S(string key) => _service.Get(key);
    public static string F(string key, params object?[] args) => _service.Format(key, args);
}
