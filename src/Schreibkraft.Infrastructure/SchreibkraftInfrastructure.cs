using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NAudio.Wave;
using Schreibkraft.Core;

namespace Schreibkraft.Infrastructure;

public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly string LegacyName = string.Concat("Ma", "gic");
    private static readonly string LegacyNameWithSuffix = string.Concat(LegacyName, "-Voice");
    private static readonly string LegacyNameCompact = string.Concat(LegacyName, "Voice");
    private static readonly string LegacyNameSpaced = string.Concat(LegacyName, " Voice");
    private static readonly byte[] CurrentEntropy = Encoding.UTF8.GetBytes("Schreibkraft.v1");
    private static readonly byte[][] SupportedEntropies =
    [
        CurrentEntropy,
        Encoding.UTF8.GetBytes($"{LegacyNameWithSuffix}.v1"),
        Encoding.UTF8.GetBytes($"{LegacyNameCompact}.v1"),
        Encoding.UTF8.GetBytes($"{LegacyNameSpaced}.v1")
    ];

    public string Protect(string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(secret);
        var protectedBytes = ProtectedData.Protect(bytes, CurrentEntropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
        {
            return string.Empty;
        }

        var bytes = Convert.FromBase64String(protectedSecret);
        CryptographicException? lastError = null;
        foreach (var entropy in SupportedEntropies)
        {
            try
            {
                var plainBytes = ProtectedData.Unprotect(bytes, entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException ex)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new CryptographicException("Secret konnte nicht entschlüsselt werden.");
    }
}

public sealed class SettingsService : ISettingsService
{
    private static readonly string LegacyDataFolderName = string.Concat("Ma", "gic", "-Voice");

    private readonly IAppProfile _profile;
    private readonly string? _legacyDataDirectory;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private bool _legacyMigrationAttempted;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string DataDirectory { get; }
    public string LogDirectory { get; }
    public string SettingsPath { get; }

    public SettingsService(IAppProfile profile, string? dataDirectory = null)
        : this(profile, dataDirectory, null)
    {
    }

    internal SettingsService(IAppProfile profile, string? dataDirectory, string? legacyDataDirectory)
    {
        _profile = profile;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        DataDirectory = dataDirectory ?? Path.Combine(localAppData, profile.DataFolderName);
        _legacyDataDirectory = legacyDataDirectory
            ?? (dataDirectory is null && profile.DataFolderName.Equals("Schreibkraft", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(localAppData, LegacyDataFolderName)
                : null);
        LogDirectory = Path.Combine(DataDirectory, "logs");
        SettingsPath = Path.Combine(DataDirectory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        MigrateLegacyDataDirectoryIfNeeded();
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);

        if (!File.Exists(SettingsPath))
        {
            var defaults = new AppSettings { Assistants = _profile.CreateDefaultAssistants() };
            await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false) ?? new AppSettings();
            Normalize(settings);
            return settings;
        }
        catch (JsonException)
        {
            var backupPath = $"{SettingsPath}.defekt-{DateTimeOffset.Now:yyyyMMddHHmmss}.bak";
            File.Move(SettingsPath, backupPath, overwrite: true);
            var defaults = new AppSettings { Assistants = _profile.CreateDefaultAssistants() };
            await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var tempPath = $"{SettingsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(LogDirectory);
            Normalize(settings);

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, SettingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            _saveGate.Release();
        }
    }

    private void MigrateLegacyDataDirectoryIfNeeded()
    {
        if (_legacyMigrationAttempted)
        {
            return;
        }

        _legacyMigrationAttempted = true;

        if (string.IsNullOrWhiteSpace(_legacyDataDirectory)
            || !Directory.Exists(_legacyDataDirectory)
            || PathsEqual(_legacyDataDirectory, DataDirectory))
        {
            return;
        }

        CopyMissingFiles(_legacyDataDirectory, DataDirectory);
    }

    private static void CopyMissingFiles(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory))
        {
            var targetFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
            if (!File.Exists(targetFile))
            {
                File.Copy(sourceFile, targetFile);
            }
        }

        foreach (var sourceSubDirectory in Directory.EnumerateDirectories(sourceDirectory))
        {
            var targetSubDirectory = Path.Combine(targetDirectory, Path.GetFileName(sourceSubDirectory));
            CopyMissingFiles(sourceSubDirectory, targetSubDirectory);
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        var fullLeft = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullRight = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullLeft, fullRight, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Providers that run locally and don't need an API key (Bearer header can be a placeholder).</summary>
    private static bool IsKeylessProvider(string provider) =>
        provider.Equals(Defaults.OllamaProviderName, StringComparison.OrdinalIgnoreCase)
        || provider.Equals(Defaults.LmStudioProviderName, StringComparison.OrdinalIgnoreCase);

    public ReadinessReport Validate(AppSettings settings)
    {
        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(settings.SttProvider))
        {
            issues.Add(new("sttProvider", L.S("validation.stt_provider")));
        }
        else if (!Defaults.TryGetSttEndpoint(settings.SttProvider, settings.SttEndpointOverride, out _))
        {
            // Either truly unsupported or a provider that needs a URL the user hasn't supplied yet.
            var needsEndpoint =
                settings.SttProvider.Equals(Defaults.OpenAiCompatibleProviderName, StringComparison.OrdinalIgnoreCase)
                || settings.SttProvider.Equals(Defaults.AzureSpeechProviderName, StringComparison.OrdinalIgnoreCase);
            issues.Add(new("sttProvider",
                needsEndpoint ? L.S("validation.custom_endpoint_missing") : L.S("validation.stt_unsupported")));
        }

        if (string.IsNullOrWhiteSpace(settings.SttModel))
        {
            issues.Add(new("sttModel", L.S("validation.stt_model")));
        }

        if (!settings.HasEncryptedSttApiKey && !IsKeylessProvider(settings.SttProvider))
        {
            issues.Add(new("sttApiKey", L.S("validation.stt_api_key")));
        }

        if (string.IsNullOrWhiteSpace(settings.LlmProvider))
        {
            issues.Add(new("llmProvider", L.S("validation.llm_provider")));
        }
        else if (!Defaults.TryGetLlmEndpoint(settings.LlmProvider, settings.LlmEndpointOverride, settings.LlmModel, out _))
        {
            issues.Add(new("llmProvider",
                settings.LlmProvider.Equals(Defaults.OpenAiCompatibleProviderName, StringComparison.OrdinalIgnoreCase)
                    ? L.S("validation.custom_endpoint_missing")
                    : L.S("validation.llm_unsupported")));
        }

        if (string.IsNullOrWhiteSpace(settings.LlmModel))
        {
            issues.Add(new("llmModel", L.S("validation.llm_model")));
        }

        if (!settings.HasEncryptedLlmApiKey && !IsKeylessProvider(settings.LlmProvider))
        {
            issues.Add(new("llmApiKey", L.S("validation.llm_api_key")));
        }

        if (settings.Assistants.Count == 0)
        {
            issues.Add(new("assistants", L.S("validation.no_assistants")));
        }
        else
        {
            foreach (var assistant in settings.Assistants)
            {
                var label = string.IsNullOrWhiteSpace(assistant.Name) ? assistant.Type.ToString() : assistant.Name;
                if (!HotkeyParser.TryParse(assistant.Hotkey, out _, out var hotkeyError))
                {
                    issues.Add(new($"hotkey.{assistant.Id}", $"{label}: {hotkeyError}"));
                }
            }
        }

        return new ReadinessReport(issues.Count == 0 ? TrayStatus.Idle : TrayStatus.ConfigurationRequired, issues);
    }

    private void Normalize(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.InputLanguage))
        {
            settings.InputLanguage = "de";
        }

        if (string.IsNullOrWhiteSpace(settings.OutputLanguage))
        {
            settings.OutputLanguage = Defaults.SameAsInputLanguageCode;
        }

        if (string.IsNullOrWhiteSpace(settings.AudioInputDeviceId))
        {
            settings.AudioInputDeviceId = Defaults.DefaultAudioInputDeviceId;
        }

        if (string.IsNullOrWhiteSpace(settings.LastSelectedSettingsSection))
        {
            settings.LastSelectedSettingsSection = "overview";
        }

        settings.TranscriptionRetriesOnFailure = Math.Clamp(settings.TranscriptionRetriesOnFailure, 0, 5);
        settings.LlmRetriesOnFailure = Math.Clamp(settings.LlmRetriesOnFailure, 0, 5);
        settings.ClipboardInsertRetriesOnFailure = Math.Clamp(settings.ClipboardInsertRetriesOnFailure, 0, 5);
        settings.RecordingSoundVolumePercent = Math.Clamp(settings.RecordingSoundVolumePercent, 0, 100);

        // Eine leere Liste (`[]`) ist faktisch wie "kein Assistent konfiguriert" — die App
        // ist dann ohne Wirkung und der User würde im UI eine leere Hotkey-Seite sehen.
        // ??= würde nur null ersetzen, nicht eine leere Liste, daher hier explizit prüfen.
        if (settings.Assistants is null || settings.Assistants.Count == 0)
        {
            settings.Assistants = _profile.CreateDefaultAssistants();
        }

        settings.SpellingCorrectionSets ??= new List<SpellingCorrectionSet>();
        foreach (var set in settings.SpellingCorrectionSets)
        {
            if (string.IsNullOrWhiteSpace(set.Id))
            {
                set.Id = Guid.NewGuid().ToString("N");
            }
            set.Name ??= string.Empty;
            set.Replacements ??= new List<SpellingReplacement>();
            set.Replacements.RemoveAll(r => r is null);
            foreach (var r in set.Replacements)
            {
                r.From ??= string.Empty;
                r.To ??= string.Empty;
            }
            set.Terms ??= new List<string>();
            // Migration: an earlier UI version could persist multiple terms joined by '\r' inside
            // a single string. Split them apart, then trim and drop empties.
            set.Terms = set.Terms
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .SelectMany(t => t.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();
        }
        var knownSetIds = new HashSet<string>(settings.SpellingCorrectionSets.Select(s => s.Id));
        foreach (var assistant in settings.Assistants)
        {
            assistant.EnabledSpellingSetIds ??= new List<string>();
            assistant.EnabledSpellingSetIds.RemoveAll(id => string.IsNullOrEmpty(id) || !knownSetIds.Contains(id));
        }
        foreach (var assistant in settings.Assistants)
        {
            if (string.IsNullOrWhiteSpace(assistant.Id))
            {
                assistant.Id = Guid.NewGuid().ToString("N");
            }

            var typeDefinition = _profile.Modes.FirstOrDefault(m => m.Mode == assistant.Type);
            if (typeDefinition is null)
            {
                assistant.Type = AssistantMode.Transform;
                typeDefinition = _profile.Modes.FirstOrDefault(m => m.Mode == assistant.Type);
                if (typeDefinition is null)
                {
                    continue;
                }
            }

            if (!Enum.IsDefined(assistant.ParagraphDensity))
            {
                assistant.ParagraphDensity = ParagraphDensity.Balanced;
            }

            if (!Enum.IsDefined(assistant.EmojiExpression))
            {
                assistant.EmojiExpression = EmojiExpression.Balanced;
            }

            assistant.EnabledSpellingSetIds ??= new List<string>();

            if (string.IsNullOrWhiteSpace(assistant.Name))
            {
                assistant.Name = typeDefinition.Name;
            }

            if (string.IsNullOrWhiteSpace(assistant.Prompt))
            {
                assistant.Prompt = typeDefinition.DefaultPrompt;
            }
            else
            {
                assistant.Prompt = AssistantPromptDefaults.NormalizePromptForMode(_profile, assistant.Type, assistant.Prompt);
            }

            assistant.SystemPromptOverride = string.IsNullOrWhiteSpace(assistant.SystemPromptOverride)
                ? null
                : assistant.SystemPromptOverride.Trim();

            assistant.InputLanguageOverride = NormalizeLanguageOverride(
                assistant.InputLanguageOverride,
                Defaults.InputLanguages.Select(l => l.Code));

            assistant.OutputLanguageOverride = NormalizeLanguageOverride(
                assistant.OutputLanguageOverride,
                Defaults.OutputLanguages.Select(l => l.Code));

            assistant.Intensity = Math.Clamp(assistant.Intensity, Defaults.MinModeIntensity, Defaults.MaxModeIntensity);
        }
    }

    private static string? NormalizeLanguageOverride(string? value, IEnumerable<string> validCodes)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return validCodes.Any(code => code.Equals(value, StringComparison.OrdinalIgnoreCase)) ? value : null;
    }

}

public sealed class NAudioRecorder(ISettingsService settingsService, ILogger<NAudioRecorder>? logger = null) : IAudioRecorder
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private DateTimeOffset _startedAt;
    private readonly WaveFormat _format = new(16000, 16, 1);

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_waveIn is not null)
        {
            return;
        }

        var settings = await settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        _buffer = new MemoryStream();
        _startedAt = DateTimeOffset.UtcNow;
        _waveIn = new WaveInEvent
        {
            DeviceNumber = ResolveDeviceNumber(settings.AudioInputDeviceId),
            WaveFormat = _format,
            BufferMilliseconds = 50,
            NumberOfBuffers = 3
        };
        _waveIn.DataAvailable += (_, args) => _buffer?.Write(args.Buffer, 0, args.BytesRecorded);
        _waveIn.RecordingStopped += (_, args) =>
        {
            if (args.Exception is not null)
            {
                logger?.LogError(args.Exception, L.S("audio.recording_failed"));
            }
        };
        try
        {
            _waveIn.StartRecording();
        }
        catch (Exception ex) when (IsMicrophoneStartupException(ex))
        {
            _waveIn.Dispose();
            _waveIn = null;
            _buffer.Dispose();
            _buffer = null;
            throw new InvalidOperationException(L.S("mic.access_failed_long"), ex);
        }

    }

    private static int ResolveDeviceNumber(string deviceId)
    {
        if (string.Equals(deviceId, Defaults.DefaultAudioInputDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        return int.TryParse(deviceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deviceNumber)
            ? deviceNumber
            : -1;
    }

    private static bool IsMicrophoneStartupException(Exception ex) =>
        ex is InvalidOperationException or UnauthorizedAccessException
        || ex.GetType().Name.Contains("MmException", StringComparison.OrdinalIgnoreCase);

    public Task<AudioBuffer> StopAsync(CancellationToken cancellationToken = default)
    {
        if (_waveIn is null || _buffer is null)
        {
            return Task.FromResult(new AudioBuffer([], 16000, 1, TimeSpan.Zero));
        }

        _waveIn.StopRecording();
        _waveIn.Dispose();
        _waveIn = null;

        var bytes = _buffer.ToArray();
        _buffer.Dispose();
        _buffer = null;
        return Task.FromResult(new AudioBuffer(bytes, 16000, 1, DateTimeOffset.UtcNow - _startedAt));
    }

    public Task AbortAsync(CancellationToken cancellationToken = default)
    {
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;
        _buffer?.Dispose();
        _buffer = null;
        return Task.CompletedTask;
    }
}

public sealed class NAudioDeviceService : IAudioDeviceService
{
    public IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        var devices = new List<AudioInputDevice>
        {
            new(Defaults.DefaultAudioInputDeviceId, L.S("language.auto"), true)
        };

        for (var index = 0; index < WaveIn.DeviceCount; index++)
        {
            var capabilities = WaveIn.GetCapabilities(index);
            devices.Add(new(index.ToString(CultureInfo.InvariantCulture), capabilities.ProductName, false));
        }

        return devices;
    }
}

public sealed class OpenAiCompatibleSttService(HttpClient httpClient) : ISttService
{
    public async Task<string> TranscribeAsync(SttRequest request, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(request.Model), "model");
        if (!Defaults.IsAutoLanguage(request.Language))
        {
            form.Add(new StringContent(request.Language), "language");
        }

        form.Add(new StringContent("json"), "response_format");

        var wavBytes = WavWriter.ToWav(request.Audio);
        var audioContent = new ByteArrayContent(wavBytes);
        audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        form.Add(audioContent, "file", "aufnahme.wav");

        using var message = new HttpRequestMessage(HttpMethod.Post, request.Endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        message.Content = form;

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<SttResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return payload?.Text?.Trim() ?? string.Empty;
    }

    private sealed record SttResponse([property: JsonPropertyName("text")] string? Text);
}

public sealed class OpenAiCompatibleLlmService(HttpClient httpClient) : ILlmService
{
    public async Task<string> ProcessAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            model = request.Model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = $"{request.ModePrompt}\n\nTranskript:\n{request.Transcript}" }
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, request.Endpoint);
        // Local backends (Ollama, LM Studio) often run without a key; skip the header in that case.
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        }
        message.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<ChatResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? string.Empty;
    }

    private sealed record ChatResponse([property: JsonPropertyName("choices")] Choice[]? Choices);
    private sealed record Choice([property: JsonPropertyName("message")] ChatMessage? Message);
    private sealed record ChatMessage([property: JsonPropertyName("content")] string? Content);
}

/// <summary>Anthropic Claude (Messages API). Uses x-api-key header and a system parameter outside the messages array.</summary>
public sealed class AnthropicLlmService(HttpClient httpClient) : ILlmService
{
    private const string ApiVersion = "2023-06-01";

    public async Task<string> ProcessAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            model = request.Model,
            max_tokens = 4096,
            temperature = 0.2,
            system = request.SystemPrompt,
            messages = new object[]
            {
                new { role = "user", content = $"{request.ModePrompt}\n\nTranskript:\n{request.Transcript}" }
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, request.Endpoint);
        message.Headers.Add("x-api-key", request.ApiKey);
        message.Headers.Add("anthropic-version", ApiVersion);
        message.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<MessagesResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var textParts = payload?.Content?.Where(c => c.Type == "text").Select(c => c.Text ?? string.Empty);
        return textParts is null ? string.Empty : string.Concat(textParts).Trim();
    }

    private sealed record MessagesResponse([property: JsonPropertyName("content")] ContentBlock[]? Content);
    private sealed record ContentBlock(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] string? Text);
}

/// <summary>Google Gemini (generateContent endpoint). Uses ?key=... query param and a different payload shape.</summary>
public sealed class GoogleGeminiLlmService(HttpClient httpClient) : ILlmService
{
    public async Task<string> ProcessAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            system_instruction = new { parts = new object[] { new { text = request.SystemPrompt } } },
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { new { text = $"{request.ModePrompt}\n\nTranskript:\n{request.Transcript}" } }
                }
            },
            generationConfig = new { temperature = 0.2 }
        };

        var url = request.Endpoint.Contains('?')
            ? $"{request.Endpoint}&key={Uri.EscapeDataString(request.ApiKey)}"
            : $"{request.Endpoint}?key={Uri.EscapeDataString(request.ApiKey)}";

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<GeminiResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var firstCandidate = payload?.Candidates?.FirstOrDefault();
        var textParts = firstCandidate?.Content?.Parts?.Select(p => p.Text ?? string.Empty);
        return textParts is null ? string.Empty : string.Concat(textParts).Trim();
    }

    private sealed record GeminiResponse([property: JsonPropertyName("candidates")] Candidate[]? Candidates);
    private sealed record Candidate([property: JsonPropertyName("content")] GeminiContent? Content);
    private sealed record GeminiContent([property: JsonPropertyName("parts")] GeminiPart[]? Parts);
    private sealed record GeminiPart([property: JsonPropertyName("text")] string? Text);
}

/// <summary>Deepgram listen endpoint for speech-to-text. Sends raw audio in body with Token auth.</summary>
public sealed class DeepgramSttService(HttpClient httpClient) : ISttService
{
    public async Task<string> TranscribeAsync(SttRequest request, CancellationToken cancellationToken = default)
    {
        var url = request.Endpoint;
        var query = new List<string> { $"model={Uri.EscapeDataString(request.Model)}", "smart_format=true", "punctuate=true" };
        if (!Defaults.IsAutoLanguage(request.Language))
        {
            query.Add($"language={Uri.EscapeDataString(request.Language)}");
        }
        url = $"{url}?{string.Join('&', query)}";

        var wavBytes = WavWriter.ToWav(request.Audio);
        var audioContent = new ByteArrayContent(wavBytes);
        audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Token", request.ApiKey);
        message.Content = audioContent;

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<DeepgramResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return payload?.Results?.Channels?.FirstOrDefault()?.Alternatives?.FirstOrDefault()?.Transcript?.Trim() ?? string.Empty;
    }

    private sealed record DeepgramResponse([property: JsonPropertyName("results")] DeepgramResults? Results);
    private sealed record DeepgramResults([property: JsonPropertyName("channels")] DeepgramChannel[]? Channels);
    private sealed record DeepgramChannel([property: JsonPropertyName("alternatives")] DeepgramAlternative[]? Alternatives);
    private sealed record DeepgramAlternative([property: JsonPropertyName("transcript")] string? Transcript);
}

/// <summary>Azure Speech Short Audio (REST): expects a region-specific endpoint URL.</summary>
public sealed class AzureSpeechSttService(HttpClient httpClient) : ISttService
{
    public async Task<string> TranscribeAsync(SttRequest request, CancellationToken cancellationToken = default)
    {
        var url = request.Endpoint;
        if (!Defaults.IsAutoLanguage(request.Language))
        {
            var sep = url.Contains('?') ? '&' : '?';
            // Azure expects BCP-47 codes (e.g. "de-DE"); we pass through what we have.
            url = $"{url}{sep}language={Uri.EscapeDataString(request.Language)}";
        }

        var wavBytes = WavWriter.ToWav(request.Audio);
        var content = new ByteArrayContent(wavBytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav; codecs=audio/pcm; samplerate=16000");

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Headers.Add("Ocp-Apim-Subscription-Key", request.ApiKey);
        message.Headers.Add("Accept", "application/json");
        message.Content = content;

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<AzureResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return payload?.DisplayText?.Trim() ?? string.Empty;
    }

    private sealed record AzureResponse([property: JsonPropertyName("DisplayText")] string? DisplayText);
}

/// <summary>AssemblyAI: upload audio, start a transcript job, poll until completion.</summary>
public sealed class AssemblyAiSttService(HttpClient httpClient) : ISttService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(2);

    public async Task<string> TranscribeAsync(SttRequest request, CancellationToken cancellationToken = default)
    {
        // 1) Upload raw audio (PCM WAV) — AssemblyAI also accepts raw bytes.
        var wavBytes = WavWriter.ToWav(request.Audio);
        using var uploadMessage = new HttpRequestMessage(HttpMethod.Post, Defaults.AssemblyAiUploadEndpoint);
        uploadMessage.Headers.Add("authorization", request.ApiKey);
        uploadMessage.Content = new ByteArrayContent(wavBytes);
        uploadMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        using var uploadResponse = await httpClient.SendAsync(uploadMessage, cancellationToken).ConfigureAwait(false);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadPayload = await JsonSerializer.DeserializeAsync<UploadResponse>(
            await uploadResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var audioUrl = uploadPayload?.UploadUrl ?? throw new InvalidOperationException("AssemblyAI: missing upload_url");

        // 2) Start transcript job.
        var startBody = new Dictionary<string, object?>
        {
            ["audio_url"] = audioUrl,
            ["speech_model"] = string.IsNullOrWhiteSpace(request.Model) ? "universal" : request.Model
        };
        if (!Defaults.IsAutoLanguage(request.Language))
        {
            startBody["language_code"] = request.Language;
        }
        using var startMessage = new HttpRequestMessage(HttpMethod.Post, request.Endpoint);
        startMessage.Headers.Add("authorization", request.ApiKey);
        startMessage.Content = new StringContent(JsonSerializer.Serialize(startBody), Encoding.UTF8, "application/json");
        using var startResponse = await httpClient.SendAsync(startMessage, cancellationToken).ConfigureAwait(false);
        startResponse.EnsureSuccessStatusCode();
        var startPayload = await JsonSerializer.DeserializeAsync<TranscriptResponse>(
            await startResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var transcriptId = startPayload?.Id ?? throw new InvalidOperationException("AssemblyAI: missing transcript id");

        // 3) Poll for completion.
        var pollUrl = $"{request.Endpoint}/{transcriptId}";
        var deadline = DateTimeOffset.UtcNow + MaxWait;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var pollMessage = new HttpRequestMessage(HttpMethod.Get, pollUrl);
            pollMessage.Headers.Add("authorization", request.ApiKey);
            using var pollResponse = await httpClient.SendAsync(pollMessage, cancellationToken).ConfigureAwait(false);
            pollResponse.EnsureSuccessStatusCode();
            var payload = await JsonSerializer.DeserializeAsync<TranscriptResponse>(
                await pollResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            switch (payload?.Status)
            {
                case "completed":
                    return payload.Text?.Trim() ?? string.Empty;
                case "error":
                    throw new InvalidOperationException($"AssemblyAI error: {payload.Error}");
            }
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("AssemblyAI: transcription did not complete within the timeout.");
            }
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record UploadResponse([property: JsonPropertyName("upload_url")] string? UploadUrl);
    private sealed record TranscriptResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("error")] string? Error);
}

/// <summary>ElevenLabs Speech-to-Text (Scribe): multipart upload, returns text directly.</summary>
public sealed class ElevenLabsSttService(HttpClient httpClient) : ISttService
{
    public async Task<string> TranscribeAsync(SttRequest request, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(string.IsNullOrWhiteSpace(request.Model) ? "scribe_v1" : request.Model), "model_id");
        if (!Defaults.IsAutoLanguage(request.Language))
        {
            form.Add(new StringContent(request.Language), "language_code");
        }

        var wavBytes = WavWriter.ToWav(request.Audio);
        var audioContent = new ByteArrayContent(wavBytes);
        audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        form.Add(audioContent, "file", "recording.wav");

        using var message = new HttpRequestMessage(HttpMethod.Post, request.Endpoint);
        message.Headers.Add("xi-api-key", request.ApiKey);
        message.Content = form;

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<ScribeResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return payload?.Text?.Trim() ?? string.Empty;
    }

    private sealed record ScribeResponse([property: JsonPropertyName("text")] string? Text);
}

/// <summary>Routes ILlmService calls to the concrete implementation based on <see cref="LlmRequest.Provider"/>.</summary>
public sealed class RoutingLlmService(
    OpenAiCompatibleLlmService openAi,
    AnthropicLlmService anthropic,
    GoogleGeminiLlmService gemini) : ILlmService
{
    public Task<string> ProcessAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Provider.Equals(Defaults.AnthropicProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return anthropic.ProcessAsync(request, cancellationToken);
        }
        if (request.Provider.Equals(Defaults.GoogleGeminiProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return gemini.ProcessAsync(request, cancellationToken);
        }
        // OpenAI and OpenAI-compatible both use the chat completions schema.
        return openAi.ProcessAsync(request, cancellationToken);
    }
}

/// <summary>Routes ISttService calls based on <see cref="SttRequest.Provider"/>.</summary>
public sealed class RoutingSttService(
    OpenAiCompatibleSttService openAi,
    DeepgramSttService deepgram,
    AzureSpeechSttService azure,
    AssemblyAiSttService assemblyAi,
    ElevenLabsSttService elevenLabs) : ISttService
{
    public Task<string> TranscribeAsync(SttRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Provider.Equals(Defaults.DeepgramProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return deepgram.TranscribeAsync(request, cancellationToken);
        }
        if (request.Provider.Equals(Defaults.AzureSpeechProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return azure.TranscribeAsync(request, cancellationToken);
        }
        if (request.Provider.Equals(Defaults.AssemblyAiProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return assemblyAi.TranscribeAsync(request, cancellationToken);
        }
        if (request.Provider.Equals(Defaults.ElevenLabsProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return elevenLabs.TranscribeAsync(request, cancellationToken);
        }
        // OpenAI and OpenAI-compatible (Groq, etc.) use the Whisper-compatible multipart schema.
        return openAi.TranscribeAsync(request, cancellationToken);
    }
}

public sealed class ClipboardInputInjector : IInputInjector
{
    public async Task InsertTextAsync(string text, InsertMethod method, bool restoreClipboard, CancellationToken cancellationToken = default)
    {
        if (method == InsertMethod.SendInput)
        {
            NativeInput.SendUnicodeText(text);
            return;
        }

        var previousText = restoreClipboard ? NativeClipboard.TryGetText() : null;

        NativeClipboard.SetText(text);
        VerifyClipboardContainsText(text);
        NativeInput.SendPasteShortcut();

        // Nach Strg+V nur eine sehr kurze Pause vor dem Zurückschreiben: soll die Nachrichtenwarteschlange einen Moment abarbeiten.
        // Ob dadurch ein Randfall mit sehr langsamen Zielen vermieden wird, ist unklar – spürbar soll es nicht sein (keine langen Delays).
        if (restoreClipboard && previousText is not null)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(true);
            try
            {
                NativeClipboard.SetText(previousText);
            }
            catch
            {
                // Die Einfügung war erfolgreich; ein Restore-Fehler soll die Pipeline nicht nachträglich scheitern lassen.
            }
        }
    }

    /// <summary>
    /// Liest die Zwischenablage nach <see cref="NativeClipboard.SetText"/> einmal zurück und prüft exakte Übereinstimmung.
    /// Weitere Versuche bei Abweichung steuert der Aufrufer (Einstellung „Wiederholungsversuche bei Fehler“).
    /// </summary>
    private static void VerifyClipboardContainsText(string expected)
    {
        var actual = NativeClipboard.TryGetText();
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(L.S("clipboard.not_expected"));
        }
    }
}

internal static class NativeClipboard
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    public static string? TryGetText()
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return null;
        }

        try
        {
            var handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public static void SetText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            throw new InvalidOperationException(L.S("clipboard.locked"));
        }

        try
        {
            EmptyClipboard();
            var bytes = (text.Length + 1) * 2;
            var handle = GlobalAlloc(GmemMoveable, (UIntPtr)bytes);
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException(L.S("clipboard.alloc_failed"));
            }

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException(L.S("clipboard.lock_failed"));
            }

            try
            {
                var data = Encoding.Unicode.GetBytes(text + '\0');
                Marshal.Copy(data, 0, pointer, data.Length);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            if (SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
            {
                throw new InvalidOperationException(L.S("clipboard.set_failed"));
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);
}

public sealed class WindowsClipboardSourceCapture : IClipboardSourceCapture
{
    public string? TryGetText() => NativeClipboard.TryGetText();
}

internal static class NativeInput
{
    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;
    private const ushort VkReturn = 0x0D;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
    private const int InputKeyboard = 1;

    public static void SendPasteShortcut()
    {
        var inputs = new[]
        {
            KeyboardInput(VkControl, 0),
            KeyboardInput(VkV, 0),
            KeyboardInput(VkV, KeyEventFKeyUp),
            KeyboardInput(VkControl, KeyEventFKeyUp)
        };
        Send(inputs);
    }

    public static void SendUnicodeText(string text)
    {
        // Zeilenumbrüche normalisieren und als echten Enter-Tastendruck schicken.
        // Sonst landen \n und \r als Unicode-Codepoints im Text – Word stellt die als Kästchen dar,
        // weil es nur \r (Absatz) bzw. VK_RETURN als Zeilentrenner akzeptiert.
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var inputs = new List<Input>(normalized.Length * 2);
        foreach (var character in normalized)
        {
            if (character == '\n')
            {
                inputs.Add(KeyboardInput(VkReturn, 0));
                inputs.Add(KeyboardInput(VkReturn, KeyEventFKeyUp));
                continue;
            }

            inputs.Add(UnicodeInput(character, 0));
            inputs.Add(UnicodeInput(character, KeyEventFKeyUp));
        }

        Send(inputs.ToArray());
    }

    private static void Send(Input[] inputs)
    {
        if (inputs.Length == 0)
        {
            return;
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                L.F("error.input_send", sent, inputs.Length, error) +
                L.S("error.input_send.causes") +
                L.S("error.input_send.middle") +
                L.S("error.input_send.tip"));
        }
    }

    private static Input KeyboardInput(ushort key, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion { Keyboard = new KeyboardInputData { VirtualKey = key, Flags = flags } }
    };

    private static Input UnicodeInput(char character, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion { Keyboard = new KeyboardInputData { ScanCode = character, Flags = flags | KeyEventFUnicode } }
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, Input[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public InputUnion Data;
    }

    // Die Win32-INPUT-Struktur ist eine Union aus MOUSEINPUT, KEYBDINPUT und HARDWAREINPUT.
    // Damit Marshal.SizeOf<Input>() die korrekte Größe (40 Bytes auf x64) liefert,
    // muss die Union alle drei Member enthalten – sonst lehnt SendInput mit Win32-Fehler 87
    // (ERROR_INVALID_PARAMETER) ab.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInputData Mouse;
        [FieldOffset(0)]
        public KeyboardInputData Keyboard;
        [FieldOffset(0)]
        public HardwareInputData Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputData
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInputData
    {
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
    }
}

/// <summary>
/// Spielt zwei kurze Sinus-Beeps (mit kurzer Hüllkurve, damit es nicht klickt) für
/// Aufnahme-Start und Aufnahme-Ende. PlaySound spielt die erzeugte WAV synchron ab; Audio-Fehler
/// werden geschluckt, damit die Aufnahme dadurch nicht stoppt.
/// </summary>
public sealed class NAudioFeedbackSoundService(ISettingsService settingsService) : IFeedbackSoundService
{
    private const uint SndSync = 0x0000;
    private const uint SndMemory = 0x0004;
    private const uint SndNodefault = 0x0002;

    public async Task PlayRecordingStartAsync(CancellationToken cancellationToken = default)
    {
        var volume = await GetVolumePercentAsync(cancellationToken).ConfigureAwait(false);
        if (volume > 0)
        {
            await PlayAsync(880, 160, volume, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task PlayRecordingStopAsync(CancellationToken cancellationToken = default)
    {
        var volume = await GetVolumePercentAsync(cancellationToken).ConfigureAwait(false);
        if (volume > 0)
        {
            await PlayAsync(587, 200, volume, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<int> GetVolumePercentAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
            return settings.PlayRecordingSounds
                ? Math.Clamp(settings.RecordingSoundVolumePercent, 0, 100)
                : 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return 0;
        }
    }

    private async Task PlayAsync(double frequencyHz, int durationMs, int volumePercent, CancellationToken cancellationToken)
    {
        try
        {
            var wav = BuildBeepWav(frequencyHz, durationMs, gain: 0.35 * Math.Clamp(volumePercent, 0, 100) / 100.0);
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var handle = GCHandle.Alloc(wav, GCHandleType.Pinned);
                try
                {
                    if (!PlaySound(handle.AddrOfPinnedObject(), IntPtr.Zero, SndMemory | SndSync | SndNodefault))
                    {
                        TryMessageBeep();
                    }
                }
                finally
                {
                    handle.Free();
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            TryMessageBeep();
            // Kein Audio-Ausgabegerät verfügbar oder exklusiv blockiert – darf die Aufnahme nicht behindern.
        }
    }

    private static void TryMessageBeep()
    {
        try
        {
            MessageBeep(0xFFFFFFFF);
        }
        catch
        {
        }
    }

    private static byte[] BuildBeepWav(double frequencyHz, int durationMs, double gain)
    {
        const int sampleRate = 44100;
        const short bitsPerSample = 16;
        const short channels = 1;
        const short blockAlign = channels * bitsPerSample / 8;
        const int byteRate = sampleRate * blockAlign;
        var totalSamples = sampleRate * durationMs / 1000;
        var dataBytes = totalSamples * blockAlign;
        var fadeSamples = sampleRate * 8 / 1000;
        using var output = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);

        for (var i = 0; i < totalSamples; i++)
        {
            var sine = Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate);
            var envelope = 1.0;
            if (i < fadeSamples)
            {
                envelope = (double)i / fadeSamples;
            }
            else if (i > totalSamples - fadeSamples)
            {
                envelope = (double)(totalSamples - i) / fadeSamples;
            }

            var sample = (short)Math.Clamp(sine * envelope * gain * short.MaxValue, short.MinValue, short.MaxValue);
            writer.Write(sample);
        }

        return output.ToArray();
    }

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", ExactSpelling = true, SetLastError = true)]
    private static extern bool PlaySound(IntPtr pszSound, IntPtr hmod, uint fdwSound);

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);
}

public sealed class WindowsAutostartService(IAppProfile profile) : IAutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(profile.AutostartRegistryValueName) is string;
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (!enabled)
        {
            key.DeleteValue(profile.AutostartRegistryValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            key.SetValue(profile.AutostartRegistryValueName, $"\"{exePath}\"");
        }
    }
}

public sealed class LowLevelKeyboardHotkeyService : IHotkeyService
{
    private readonly Dictionary<string, HotkeyGesture> _gestures = [];
    private readonly HashSet<string> _pressedAssistants = [];
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId;
    private bool _paused;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyDown;
    public event EventHandler<HotkeyPressedEventArgs>? HotkeyUp;
    public bool IsPaused => _paused;

    public LowLevelKeyboardHotkeyService()
    {
        _proc = HookCallback;
    }

    public Task<IReadOnlyList<ValidationIssue>> RegisterAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _gestures.Clear();
        var issues = new List<ValidationIssue>();
        foreach (var assistant in settings.Assistants)
        {
            // Ein leeres Hotkey-Feld bedeutet "noch nicht zugewiesen" (z. B. neuer Assistent direkt
            // nach dem Anlegen). Das ist KEIN Registrierungsfehler — die Validate-Prüfung markiert
            // das ohnehin als "Einrichtung erforderlich". Erst eine syntaktisch ungültige Eingabe
            // (Konflikt, Tippfehler) ist hier ein Fehlerfall.
            if (string.IsNullOrWhiteSpace(assistant.Hotkey))
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(assistant.Name) ? assistant.Type.ToString() : assistant.Name;
            if (!HotkeyParser.TryParse(assistant.Hotkey, out var gesture, out var error))
            {
                issues.Add(new($"hotkey.{assistant.Id}", $"{label}: {error}"));
                continue;
            }

            _gestures[assistant.Id] = gesture;
        }

        if (_hookId == IntPtr.Zero)
        {
            _hookId = SetHook(_proc);
            if (_hookId == IntPtr.Zero)
            {
                issues.Add(new("hotkeys", L.S("hotkey.register_failed")));
            }
        }

        return Task.FromResult<IReadOnlyList<ValidationIssue>>(issues);
    }

    public void Pause()
    {
        _paused = true;
        _pressedAssistants.Clear();
    }

    public void Resume() => _paused = false;

    public ValueTask DisposeAsync()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        return ValueTask.CompletedTask;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_paused)
        {
            var message = (int)wParam;
            var keyCode = Marshal.ReadInt32(lParam);
            var key = VirtualKeyToName(keyCode);
            var isDown = message is WmKeyDown or WmSysKeyDown;
            var isUp = message is WmKeyUp or WmSysKeyUp;

            // Snapshot, da HotkeyUp-Handler den Service während der Iteration verändern könnten.
            foreach (var (assistantId, gesture) in _gestures.ToArray())
            {
                if (isDown)
                {
                    if (IsDownMatch(gesture, key) && _pressedAssistants.Add(assistantId))
                    {
                        HotkeyDown?.Invoke(this, new HotkeyPressedEventArgs(assistantId));
                    }
                }
                else if (isUp && _pressedAssistants.Contains(assistantId) && IsReleaseRelevant(gesture, keyCode, key))
                {
                    _pressedAssistants.Remove(assistantId);
                    HotkeyUp?.Invoke(this, new HotkeyPressedEventArgs(assistantId));
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool IsDownMatch(HotkeyGesture gesture, string key)
    {
        return key.Equals(gesture.Key, StringComparison.OrdinalIgnoreCase)
            && IsPressed(VkControl) == gesture.Control
            && IsPressed(VkMenu) == gesture.Alt
            && IsPressed(VkShift) == gesture.Shift
            && (IsPressed(VkLWin) || IsPressed(VkRWin)) == gesture.Windows;
    }

    /// <summary>
    /// Beim Loslassen reicht es, dass entweder die Haupttaste oder eine der erforderlichen Zusatztasten
    /// der laufenden Geste losgelassen wird. So endet die Aufnahme auch dann zuverlässig, wenn der Nutzer
    /// die Tasten in beliebiger Reihenfolge oder nahezu gleichzeitig loslässt.
    /// </summary>
    private static bool IsReleaseRelevant(HotkeyGesture gesture, int virtualKey, string keyName)
    {
        if (keyName.Equals(gesture.Key, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return virtualKey switch
        {
            VkControl or VkLControl or VkRControl => gesture.Control,
            VkMenu or VkLMenu or VkRMenu => gesture.Alt,
            VkShift or VkLShift or VkRShift => gesture.Shift,
            VkLWin or VkRWin => gesture.Windows,
            _ => false
        };
    }

    private static string VirtualKeyToName(int virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return $"F{virtualKey - 0x6F}";
        }

        return virtualKey switch
        {
            0x20 => "Space",
            0x0D => "Enter",
            0x09 => "Tab",
            0x1B => "Esc",
            _ => virtualKey.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static bool IsPressed(int key) => (GetKeyState(key) & 0x8000) != 0;

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        return SetWindowsHookEx(WhKeyboardLl, proc, GetModuleHandle(module.ModuleName), 0);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);
}

public static class WavWriter
{
    public static byte[] ToWav(AudioBuffer audio)
    {
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true))
        {
            var byteRate = audio.SampleRate * audio.Channels * 2;
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + audio.PcmBytes.Length);
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)audio.Channels);
            writer.Write(audio.SampleRate);
            writer.Write(byteRate);
            writer.Write((short)(audio.Channels * 2));
            writer.Write((short)16);
            writer.Write("data"u8.ToArray());
            writer.Write(audio.PcmBytes.Length);
            writer.Write(audio.PcmBytes);
        }

        return output.ToArray();
    }
}

public sealed class FileLogger
{
    private readonly string _logDirectory;
    public string CurrentLogPath => Path.Combine(_logDirectory, $"{DateTimeOffset.Now:yyyy-MM-dd}.log");

    public FileLogger(ISettingsService settingsService)
    {
        _logDirectory = settingsService.LogDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public Task WriteAsync(string message, CancellationToken cancellationToken = default)
    {
        var line = $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}";
        return File.AppendAllTextAsync(CurrentLogPath, line, cancellationToken);
    }
}
