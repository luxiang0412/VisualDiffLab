using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using VisualDiffLab.App.Services;
using VisualDiffLab.Core.Models;
using VisualDiffLab.Imaging;
using Windows.System.UserProfile;

namespace VisualDiffLab.App.Localization;

public enum AppLanguage
{
    System,
    English,
    Chinese,
}

/// <summary>
/// In-app UI localization. The indexer is consumed by classic XAML bindings so a
/// language switch updates the whole visual tree without recreating the window.
/// Dictionaries are embedded JSON files; both are validated to have the same keys.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Dictionary<string, string> English = LoadDictionary("en-US");
    private static readonly Dictionary<string, string> Chinese = LoadDictionary("zh-CN");

    private AppLanguage _language = AppLanguage.System;

    static LocalizationService()
    {
        ValidateDictionaries();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageIndex)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectiveCulture)));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Index used by the language ComboBox (System, English, 中文).</summary>
    public int LanguageIndex => (int)_language;

    public CultureInfo EffectiveCulture => ResolveCulture(_language);

    public bool IsChinese => EffectiveCulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);

    public string this[string key] => Translate(key);

    public string Translate(string key)
    {
        Dictionary<string, string> dictionary = IsChinese ? Chinese : English;
        if (dictionary.TryGetValue(key, out string? value))
        {
            return value;
        }

        return English.TryGetValue(key, out string? fallback) ? fallback : key;
    }

    public string Format(string key, params object?[] args) =>
        string.Format(EffectiveCulture, Translate(key), args);

    /// <summary>Maps an imaging error to a localized, user-facing message.</summary>
    public string ImageLoadMessage(ImageLoadErrorKind kind) => kind switch
    {
        ImageLoadErrorKind.UnsupportedFormat => Translate("Error_Image_Unsupported"),
        ImageLoadErrorKind.FileNotFound => Translate("Error_Image_FileNotFound"),
        ImageLoadErrorKind.AccessDenied => Translate("Error_Image_AccessDenied"),
        ImageLoadErrorKind.CorruptFile => Translate("Error_Image_Corrupt"),
        ImageLoadErrorKind.TooLarge => Format("Error_Image_TooLarge", ImageDocument.MaxPixelCount),
        ImageLoadErrorKind.EmptyImage => Translate("Error_Image_Empty"),
        _ => Translate("Error_Image_Unknown"),
    };

    public void SetLanguage(string? cultureName)
    {
        Language = cultureName?.ToLowerInvariant() switch
        {
            "en" or "en-us" => AppLanguage.English,
            "zh" or "zh-cn" or "zh-hans" => AppLanguage.Chinese,
            _ => AppLanguage.System,
        };
    }

    private static CultureInfo ResolveCulture(AppLanguage language) => language switch
    {
        AppLanguage.English => CultureInfo.GetCultureInfo("en-US"),
        AppLanguage.Chinese => CultureInfo.GetCultureInfo("zh-CN"),
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo("zh-CN")
            : CultureInfo.GetCultureInfo("en-US"),
    };

    private static Dictionary<string, string> LoadDictionary(string cultureName)
    {
        Assembly assembly = typeof(LocalizationService).Assembly;
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith($".Localization.{cultureName}.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded localization resource for '{cultureName}' was not found.");
        }

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Localization resource '{resourceName}' could not be opened.");
        Dictionary<string, string>? dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        return dictionary ?? throw new InvalidOperationException($"Localization resource '{resourceName}' is invalid.");
    }

    private static void ValidateDictionaries()
    {
        foreach (string missingInChinese in English.Keys.Except(Chinese.Keys))
        {
            DiagnosticsLog.Shared.Error($"Localization key missing in zh-CN: {missingInChinese}", null);
        }

        foreach (string missingInEnglish in Chinese.Keys.Except(English.Keys))
        {
            DiagnosticsLog.Shared.Error($"Localization key missing in en-US: {missingInEnglish}", null);
        }
    }
}
