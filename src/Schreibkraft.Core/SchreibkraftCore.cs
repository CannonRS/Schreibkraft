using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Schreibkraft.Core;

public enum AssistantMode
{
    /// <summary>Transkript + Anweisung aus der UI (Korrektur, Umschreiben, Stil usw.).</summary>
    Transform,
    /// <summary>Nur gesprochene Anweisung, kein Quelltext.</summary>
    Generate,
    /// <summary>Zwischenablage als Quelltext + gesprochene Anweisung.</summary>
    AnswerClipboard
}

/// <summary>Wie stark der Text mit Absätzen gegliedert werden soll (UI).</summary>
public enum ParagraphDensity
{
    Compact,
    Balanced,
    Spacious
}

public enum WritingStyle
{
    Casual,
    Neutral,
    Professional,
    Academic
}

/// <summary>Wie stark Emojis und „Social“-Ausdruck genutzt werden sollen (UI).</summary>
public enum EmojiExpression
{
    None,
    Sparse,
    Balanced,
    Lively,
    Heavy
}

public enum TrayStatus
{
    Idle,
    Paused,
    Recording,
    Processing,
    Success,
    Error,
    /// <summary>
    /// Hinweis ohne blockierende „Einrichtung“ (z. B. Speichern empfohlen) – Tastenkürzel bleiben nutzbar.
    /// </summary>
    Attention,
    ConfigurationRequired
}

public enum InsertMethod
{
    Clipboard,
    SendInput
}

public sealed record AssistantModeDefinition(
    AssistantMode Mode,
    string Name,
    string Description,
    string DefaultHotkey,
    string DefaultPrompt,
    bool RequiresClipboardSource = false);

/// <summary>Vorgefertigte Anweisungs-Vorlage, die der Nutzer per Dropdown in eine Assistenten-Karte übernehmen kann.</summary>
public sealed record PromptTemplate(
    string Name,
    string Description,
    string Text,
    IReadOnlyList<AssistantMode> CompatibleModes);

public sealed record AudioBuffer(byte[] PcmBytes, int SampleRate, int Channels, TimeSpan Duration)
{
    public bool IsEmpty => PcmBytes.Length == 0 || Duration <= TimeSpan.Zero;
}

public sealed record AudioInputDevice(string Id, string Name, bool IsDefault)
{
    public override string ToString() => Name;
}

public sealed record LanguageOption(string Code, string Name)
{
    public override string ToString() => Name;
}

public sealed record SttRequest(AudioBuffer Audio, string Provider, string Endpoint, string Model, string Language, string ApiKey);

public sealed record LlmRequest(string Transcript, AssistantMode Mode, string Provider, string Endpoint, string Model, string ApiKey, string SystemPrompt, string ModePrompt, string? SourceText = null);

public sealed record PipelineResult(bool Success, string Message, string? Transcript = null, string? FinalText = null, Exception? Error = null)
{
    public static PipelineResult Ok(string message, string? transcript, string? finalText) => new(true, message, transcript, finalText);

    public static PipelineResult Failed(string message, Exception? error = null, string? transcript = null, string? finalText = null) =>
        new(false, message, transcript, finalText, error);
}

public sealed class AppSettings
{
    public string SttProvider { get; set; } = Defaults.OpenAiProviderName;
    public string SttModel { get; set; } = "gpt-4o-mini-transcribe";
    public string InputLanguage { get; set; } = "de";
    public string OutputLanguage { get; set; } = "same";
    public string AudioInputDeviceId { get; set; } = Defaults.DefaultAudioInputDeviceId;
    public string? SttApiKeyEncrypted { get; set; }
    public string LlmProvider { get; set; } = Defaults.OpenAiProviderName;
    public string LlmModel { get; set; } = Defaults.DefaultLlmModel;
    public string? LlmApiKeyEncrypted { get; set; }
    /// <summary>Optional endpoint override (used by OpenAI-compatible custom providers, e.g. Groq/DeepSeek/Ollama).</summary>
    public string? LlmEndpointOverride { get; set; }
    /// <summary>Optional endpoint override for STT (used by OpenAI-compatible custom providers).</summary>
    public string? SttEndpointOverride { get; set; }
    public int RecordingMaxSeconds { get; set; } = 60;
    public int MinimumRecordingMilliseconds { get; set; } = 300;
    public int ProcessingTimeoutSeconds { get; set; } = 45;
    /// <summary>Zusätzliche Versuche bei fehlgeschlagener Transkription (API-/Netzwerkfehler). 0 = nur ein Versuch; höchstens 5 (siehe Normalize).</summary>
    public int TranscriptionRetriesOnFailure { get; set; } = 0;
    /// <summary>Zusätzliche Versuche bei fehlgeschlagener KI-Verarbeitung, bevor das Transkript eingefügt wird. 0 = nur ein Versuch; höchstens 5.</summary>
    public int LlmRetriesOnFailure { get; set; } = 0;
    /// <summary>Zusätzliche Versuche bei fehlgeschlagenem Einfügen über die Zwischenablage, bevor „direkt tippen“ genutzt wird. 0 = nur ein Versuch; höchstens 5.</summary>
    public int ClipboardInsertRetriesOnFailure { get; set; } = 0;
    public InsertMethod InsertMethod { get; set; } = InsertMethod.SendInput;
    public bool RestoreClipboard { get; set; } = true;
    public string LogLevel { get; set; } = "Information";
    public bool LaunchMinimizedToTray { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool PlayRecordingSounds { get; set; } = true;
    public int RecordingSoundVolumePercent { get; set; } = 100;
    public bool ShowNotifications { get; set; } = true;
    public bool OpenSettingsOnConfigurationError { get; set; } = true;
    /// <summary>Wenn true, werden die letzten Verarbeitungen (Transkript + KI-Antwort) im Diagnose-Bereich angezeigt.</summary>
    public bool KeepProcessingHistory { get; set; }
    public string LastSelectedSettingsSection { get; set; } = "overview";
    /// <summary>UI language preference. Auto = follow system culture at app start.</summary>
    public UiLanguage UiLanguage { get; set; } = UiLanguage.Auto;
    public WindowBounds WindowBounds { get; set; } = new();
    public List<AssistantInstance> Assistants { get; set; } = new();
    /// <summary>
    /// Wiederverwendbare Rechtschreibkorrektur-Sets. Ein Set ist eine benannte Sammlung von
    /// Wort-Ersetzungen, die in beliebigen Assistenten aktiviert werden kann.
    /// </summary>
    public List<SpellingCorrectionSet> SpellingCorrectionSets { get; set; } = new();

    [JsonIgnore]
    public bool HasEncryptedLlmApiKey => !string.IsNullOrWhiteSpace(LlmApiKeyEncrypted);

    [JsonIgnore]
    public bool HasEncryptedSttApiKey => !string.IsNullOrWhiteSpace(SttApiKeyEncrypted);
}

public sealed class AssistantInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonConverter(typeof(AssistantModeJsonConverter))]
    public AssistantMode Type { get; set; } = AssistantMode.Transform;
    public string Name { get; set; } = string.Empty;
    public string Hotkey { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    /// <summary>
    /// Optionaler Override für die System-Nachricht an das LLM.
    /// Null/leer bedeutet: globaler Standard (aus dem App-Profil).
    /// </summary>
    public string? SystemPromptOverride { get; set; }
    /// <summary>
    /// Optionaler Override für die STT-Eingabesprache (z. B. "de", "en", "auto").
    /// Null/leer bedeutet: globaler Standard aus den App-Einstellungen.
    /// </summary>
    public string? InputLanguageOverride { get; set; }
    /// <summary>
    /// Optionaler Override für die LLM-Ausgabesprache (z. B. "same", "de", "en").
    /// Null/leer bedeutet: globaler Standard aus den App-Einstellungen.
    /// </summary>
    public string? OutputLanguageOverride { get; set; }
    public int Intensity { get; set; } = Defaults.DefaultModeIntensity;
    public WritingStyle WritingStyle { get; set; } = WritingStyle.Neutral;
    /// <summary>Absatz- und Leerzeilennutzung für die KI (UI).</summary>
    public ParagraphDensity ParagraphDensity { get; set; } = ParagraphDensity.Balanced;
    /// <summary>Emoji- und Social-Media-Ausdrucksstärke (UI).</summary>
    public EmojiExpression EmojiExpression { get; set; } = EmojiExpression.Balanced;
    /// <summary>
    /// IDs der Rechtschreibkorrektur-Sets, die für diesen Assistenten aktiv sind.
    /// Die Sets selbst werden global in <see cref="AppSettings.SpellingCorrectionSets"/> verwaltet.
    /// </summary>
    public List<string> EnabledSpellingSetIds { get; set; } = new();

    /// <summary>UI-only: whether the assistant card is currently expanded (vs collapsed to its header).</summary>
    public bool IsExpanded { get; set; } = true;
}

/// <summary>Einzelne Wort-Ersetzung, die nach der KI-Antwort auf den Text angewendet wird.</summary>
public sealed class SpellingReplacement
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

/// <summary>
/// Benannte, wiederverwendbare Sammlung für Textnachbearbeitung. Enthält exakte Wort-Ersetzungen
/// (1:1, nach der KI-Antwort) und eine Liste von Eigennamen/Fachbegriffen, die der KI als korrekt
/// geschriebene Referenz mitgegeben werden (Fuzzy-Korrektur phonetisch ähnlicher Fehltranskripte).
/// </summary>
public sealed class SpellingCorrectionSet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<SpellingReplacement> Replacements { get; set; } = new();
    /// <summary>Eigennamen/Fachbegriffe in korrekter Schreibweise. Werden in den KI-Prompt eingebettet.</summary>
    public List<string> Terms { get; set; } = new();
}

public static class PromptComposition
{
    public static string EffectiveInputLanguage(AppSettings settings, AssistantInstance assistant) =>
        string.IsNullOrWhiteSpace(assistant.InputLanguageOverride) ? settings.InputLanguage : assistant.InputLanguageOverride!;

    public static string EffectiveOutputLanguage(AppSettings settings, AssistantInstance assistant) =>
        string.IsNullOrWhiteSpace(assistant.OutputLanguageOverride) ? settings.OutputLanguage : assistant.OutputLanguageOverride!;

    public static string EffectiveBaseSystemPrompt(IAppProfile profile, AssistantInstance assistant) =>
        string.IsNullOrWhiteSpace(assistant.SystemPromptOverride) ? profile.SystemPrompt : assistant.SystemPromptOverride!;

    public static string BuildSystemPrompt(string baseSystemPrompt, string effectiveInputLanguage, string? policyBlock = null)
    {
        var inputLanguageNote = Defaults.IsAutoLanguage(effectiveInputLanguage)
            ? L.S("prompt.system.input_language.auto")
            : L.F("prompt.system.input_language.fixed", Defaults.LanguageName(effectiveInputLanguage));
        var baseWithLanguage = $"{baseSystemPrompt.TrimEnd()} {inputLanguageNote}";
        if (string.IsNullOrWhiteSpace(policyBlock))
        {
            return baseWithLanguage;
        }

        return string.Join(Environment.NewLine + Environment.NewLine, baseWithLanguage, policyBlock.Trim());
    }

    public static string OutputInstruction(string effectiveInputLanguage, string effectiveOutputLanguage)
    {
        if (effectiveOutputLanguage.Equals(Defaults.SameAsInputLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return Defaults.IsAutoLanguage(effectiveInputLanguage)
                ? L.S("prompt.output.same_as_input.auto")
                : L.F("prompt.output.same_as_input.fixed", Defaults.LanguageName(effectiveInputLanguage));
        }

        return L.F("prompt.output.translate", Defaults.LanguageName(effectiveOutputLanguage));
    }

    public static string BuildPolicyBlock(IAppProfile profile, AssistantInstance assistant, string effectiveInputLanguage, string effectiveOutputLanguage)
    {
        var intensity = Math.Clamp(assistant.Intensity, Defaults.MinModeIntensity, Defaults.MaxModeIntensity);
        var intensityBlock = profile.IntensityStepInstruction(assistant.Type, intensity) ?? string.Empty;

        var styleBlock = profile.WritingStyleInstruction(assistant.WritingStyle);
        var emojiBlock = profile.EmojiExpressionInstruction(assistant.EmojiExpression);
        var formattingBlock = FormattingInstruction(assistant.ParagraphDensity);
        var outputBlock = OutputInstruction(effectiveInputLanguage, effectiveOutputLanguage);

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { intensityBlock, styleBlock, emojiBlock, formattingBlock, outputBlock }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    public static string FormattingInstruction(ParagraphDensity density) => density switch
    {
        ParagraphDensity.Compact => L.S("paragraph.compact"),
        ParagraphDensity.Spacious => L.S("paragraph.spacious"),
        _ => L.S("paragraph.balanced")
    };

    /// <summary>
    /// Builds the glossary prompt block: a list of correctly spelled proper names/terms with the
    /// instruction to fix phonetically similar mistranscriptions. Empty when no terms are active.
    /// </summary>
    public static string BuildGlossaryBlock(IReadOnlyList<string> terms)
    {
        if (terms is null || terms.Count == 0)
        {
            return string.Empty;
        }
        return L.F("prompt.glossary", string.Join(", ", terms));
    }
}

public static class AssistantPromptDefaults
{
    private static readonly string[] LegacyTransformDefaults =
    [
        "Bearbeite das Transkript gemäß dieser Anweisung. Ohne weitere Vorgabe: Rechtschreibung und Grammatik korrigieren, leicht glätten, Aussage beibehalten.",
        "Bearbeite das Transkript gemäß dieser Anweisung. Ohne weitere Vorgabe: Rechtschreibung und Grammatik korrigieren, Aussage beibehalten. Keine Änderung der Wortwahl.",
        "Process the transcript below. Without further specification: correct spelling and grammar, keep meaning. Do not change word choice."
    ];

    public static bool IsKnownDefaultPrompt(IAppProfile profile, AssistantMode mode, string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return true;
        }

        return profile.Modes
            .Where(definition => definition.Mode == mode)
            .Select(definition => definition.DefaultPrompt)
            .Concat(mode == AssistantMode.Transform ? LegacyTransformDefaults : Array.Empty<string>())
            .Any(defaultPrompt => PromptEquals(prompt, defaultPrompt));
    }

    public static string NormalizePromptForMode(IAppProfile profile, AssistantMode mode, string? prompt)
    {
        var modeDefault = profile.Modes.FirstOrDefault(definition => definition.Mode == mode)?.DefaultPrompt
            ?? string.Empty;

        if (IsKnownDefaultPrompt(profile, mode, prompt)
            || IsKnownDefaultPromptForDifferentMode(profile, mode, prompt))
        {
            return modeDefault;
        }

        return string.IsNullOrWhiteSpace(prompt) ? modeDefault : prompt;
    }

    private static bool IsKnownDefaultPromptForDifferentMode(IAppProfile profile, AssistantMode mode, string? prompt) =>
        profile.Modes
            .Where(definition => definition.Mode != mode)
            .Any(definition => IsKnownDefaultPrompt(profile, definition.Mode, prompt));

    private static bool PromptEquals(string? left, string? right) =>
        string.Equals(NormalizePrompt(left), NormalizePrompt(right), StringComparison.Ordinal);

    private static string NormalizePrompt(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim(), @"\s+", " ");
}

/// <summary>
/// Wendet die pro Assistent konfigurierten Wort-Ersetzungen auf einen Text an.
/// Match per Word-Boundary, die Umlaute und ß berücksichtigt; case-sensitiv.
/// Leere Quellwörter werden übersprungen.
/// </summary>
/// <summary>
/// Cleans up cosmetic whitespace flaws in the final text before it is inserted:
/// collapses runs of spaces/tabs, and strips trailing whitespace at line ends.
/// Blank lines between paragraphs are preserved.
/// </summary>
public static class TextNormalizer
{
    // Multiple spaces/tabs in a row -> a single space (does not touch line breaks).
    private static readonly Regex MultipleSpaces = new(@"[ \t]{2,}", RegexOptions.Compiled);
    // Whitespace directly before a line break or at the very end of the text.
    private static readonly Regex TrailingWhitespace = new(@"[ \t]+(?=\r?\n)|[ \t]+\z", RegexOptions.Compiled);

    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = MultipleSpaces.Replace(text, " ");
        result = TrailingWhitespace.Replace(result, string.Empty);
        return result;
    }
}

public static class SpellingPostProcessor
{
    /// <summary>
    /// Sammelt alle Wort-Ersetzungen aus den vom Assistenten aktivierten Sets.
    /// Reihenfolge: in Reihenfolge der aktivierten Set-IDs, innerhalb des Sets in Definitionsreihenfolge.
    /// </summary>
    public static IEnumerable<SpellingReplacement> ResolveActiveReplacements(AppSettings settings, AssistantInstance assistant)
    {
        if (assistant.EnabledSpellingSetIds is null || assistant.EnabledSpellingSetIds.Count == 0)
        {
            yield break;
        }

        var sets = settings.SpellingCorrectionSets;
        if (sets is null || sets.Count == 0)
        {
            yield break;
        }

        foreach (var id in assistant.EnabledSpellingSetIds)
        {
            var set = sets.FirstOrDefault(s => s.Id == id);
            if (set?.Replacements is null)
            {
                continue;
            }

            foreach (var r in set.Replacements)
            {
                yield return r;
            }
        }
    }

    public static string Apply(string text, IEnumerable<SpellingReplacement>? replacements)
    {
        if (string.IsNullOrEmpty(text) || replacements is null)
        {
            return text;
        }

        var result = text;
        foreach (var pair in replacements)
        {
            if (pair is null || string.IsNullOrEmpty(pair.From))
            {
                continue;
            }

            var pattern = $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(pair.From)}(?![\p{{L}}\p{{N}}_])";
            result = Regex.Replace(result, pattern, pair.To ?? string.Empty, RegexOptions.CultureInvariant);
        }

        return result;
    }

    /// <summary>
    /// Collects the proper-name/term glossary entries from the assistant's enabled sets.
    /// Deduplicated, order-preserving, trimmed.
    /// </summary>
    public static IReadOnlyList<string> ResolveActiveTerms(AppSettings settings, AssistantInstance assistant)
    {
        var ids = assistant.EnabledSpellingSetIds;
        var sets = settings.SpellingCorrectionSets;
        if (ids is null || ids.Count == 0 || sets is null || sets.Count == 0)
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var id in ids)
        {
            var set = sets.FirstOrDefault(s => s.Id == id);
            if (set?.Terms is null)
            {
                continue;
            }
            foreach (var term in set.Terms)
            {
                var trimmed = term?.Trim();
                if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
                {
                    result.Add(trimmed);
                }
            }
        }
        return result;
    }
}

public sealed class WindowBounds
{
    public double Width { get; set; } = 900;
    public double Height { get; set; } = 650;
    public double? X { get; set; }
    public double? Y { get; set; }
    public bool IsSet => X.HasValue && Y.HasValue && Width > 0 && Height > 0;
}

public sealed record ValidationIssue(string Field, string Message);

public sealed record ReadinessReport(TrayStatus Status, IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsReady => Status == TrayStatus.Idle && Issues.Count == 0;
}

public sealed record HotkeyGesture(bool Control, bool Alt, bool Shift, bool Windows, string Key)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Control) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Windows) parts.Add("Win");
        parts.Add(Key);
        return string.Join("+", parts);
    }
}

/// <summary>App-spezifische Identität, verfügbare Assistenten-Typen und Texte. Wird per DI aus dem App-Projekt bereitgestellt.</summary>
public interface IAppProfile
{
    string AppName { get; }
    string DataFolderName { get; }
    string AuthorName { get; }
    string CopyrightText { get; }
    string LicenseName { get; }
    string MutexName { get; }
    string AumId { get; }
    string AutostartRegistryValueName { get; }
    string SystemPrompt { get; }
    /// <summary>Verfügbare Assistenten-Typen mit Default-Werten als Vorlage beim Hinzufügen neuer Assistenten.</summary>
    IReadOnlyList<AssistantModeDefinition> Modes { get; }
    /// <summary>Vorgefertigte Anweisungs-Vorlagen, die in einer Karte per Dropdown übernommen werden können.</summary>
    IReadOnlyList<PromptTemplate> PromptTemplates { get; }
    string IntensityStepName(AssistantMode mode, int intensity);
    string IntensityStepInstruction(AssistantMode mode, int intensity);
    string WritingStyleInstruction(WritingStyle style);
    string EmojiExpressionInstruction(EmojiExpression level);
    /// <summary>Liste der Assistenten beim ersten Start (oder beim Reset).</summary>
    List<AssistantInstance> CreateDefaultAssistants();
}

public static class Defaults
{
    public const string OpenAiProviderName = "OpenAI";
    public const string OpenAiCompatibleProviderName = "OpenAI-compatible";
    public const string AnthropicProviderName = "Anthropic";
    public const string GoogleGeminiProviderName = "Google Gemini";
    public const string DeepgramProviderName = "Deepgram";
    public const string AzureSpeechProviderName = "Azure Speech";
    public const string AssemblyAiProviderName = "AssemblyAI";
    public const string ElevenLabsProviderName = "ElevenLabs Scribe";
    public const string GroqProviderName = "Groq";
    public const string DeepSeekProviderName = "DeepSeek";
    public const string XaiProviderName = "xAI Grok";
    public const string OllamaProviderName = "Ollama (local)";
    public const string LmStudioProviderName = "LM Studio (local)";

    public const string DefaultAudioInputDeviceId = "default";
    public const string AutoLanguageCode = "auto";
    public const string SameAsInputLanguageCode = "same";
    public const int DefaultModeIntensity = 3;
    public const int MinModeIntensity = 1;
    public const int MaxModeIntensity = 5;
    /// <summary>Empfohlenes Standard-KI-Modell (Chat Completions); auch frei überschreibbar in der App.</summary>
    public const string DefaultLlmModel = "gpt-5.1";
    public const string OpenAiSttEndpoint = "https://api.openai.com/v1/audio/transcriptions";
    public const string OpenAiLlmEndpoint = "https://api.openai.com/v1/chat/completions";
    public const string AnthropicMessagesEndpoint = "https://api.anthropic.com/v1/messages";
    public const string GeminiGenerateContentBase = "https://generativelanguage.googleapis.com/v1beta/models";
    public const string DeepgramListenEndpoint = "https://api.deepgram.com/v1/listen";
    public const string AssemblyAiTranscriptEndpoint = "https://api.assemblyai.com/v2/transcript";
    public const string AssemblyAiUploadEndpoint = "https://api.assemblyai.com/v2/upload";
    public const string ElevenLabsScribeEndpoint = "https://api.elevenlabs.io/v1/speech-to-text";
    public const string GroqLlmEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    public const string GroqSttEndpoint = "https://api.groq.com/openai/v1/audio/transcriptions";
    public const string DeepSeekLlmEndpoint = "https://api.deepseek.com/v1/chat/completions";
    public const string XaiLlmEndpoint = "https://api.x.ai/v1/chat/completions";
    public const string OllamaDefaultLlmEndpoint = "http://localhost:11434/v1/chat/completions";
    public const string LmStudioDefaultLlmEndpoint = "http://localhost:1234/v1/chat/completions";

    /// <summary>Provider, die für Speech-to-Text ausgewählt werden können.</summary>
    public static readonly IReadOnlyList<string> KnownSttProviders =
    [
        OpenAiProviderName,
        GroqProviderName,
        DeepgramProviderName,
        AzureSpeechProviderName,
        AssemblyAiProviderName,
        ElevenLabsProviderName,
        OpenAiCompatibleProviderName
    ];

    /// <summary>Provider, die für KI-Verarbeitung ausgewählt werden können.</summary>
    public static readonly IReadOnlyList<string> KnownLlmProviders =
    [
        OpenAiProviderName,
        AnthropicProviderName,
        GoogleGeminiProviderName,
        GroqProviderName,
        DeepSeekProviderName,
        XaiProviderName,
        OllamaProviderName,
        LmStudioProviderName,
        OpenAiCompatibleProviderName
    ];

    /// <summary>Alle Provider-Namen, die wir kennen (Vereinigung von STT und LLM).</summary>
    public static readonly IReadOnlyList<string> KnownProviders =
    [
        OpenAiProviderName,
        AnthropicProviderName,
        GoogleGeminiProviderName,
        GroqProviderName,
        DeepSeekProviderName,
        XaiProviderName,
        OllamaProviderName,
        LmStudioProviderName,
        OpenAiCompatibleProviderName,
        DeepgramProviderName,
        AzureSpeechProviderName,
        AssemblyAiProviderName,
        ElevenLabsProviderName
    ];

    /// <summary>Vorschläge für Modell-IDs je Provider; manuelle Eingabe bleibt erlaubt.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ProviderLlmModelSuggestions = new Dictionary<string, IReadOnlyList<string>>
    {
        [AnthropicProviderName] = new[]
        {
            "claude-opus-4-7",
            "claude-sonnet-4-6",
            "claude-haiku-4-5"
        },
        [GoogleGeminiProviderName] = new[]
        {
            "gemini-2.5-pro",
            "gemini-2.5-flash",
            "gemini-2.0-flash",
            "gemini-2.0-flash-lite"
        },
        [GroqProviderName] = new[]
        {
            "llama-3.3-70b-versatile",
            "llama-3.1-70b-versatile",
            "llama-3.1-8b-instant",
            "mixtral-8x7b-32768",
            "gemma2-9b-it"
        },
        [DeepSeekProviderName] = new[]
        {
            "deepseek-chat",
            "deepseek-reasoner"
        },
        [XaiProviderName] = new[]
        {
            "grok-2-latest",
            "grok-2-1212",
            "grok-beta"
        },
        [OllamaProviderName] = new[]
        {
            "llama3.1:8b",
            "llama3.1:70b",
            "qwen2.5:14b",
            "qwen2.5:32b",
            "mistral:7b",
            "phi3:14b"
        },
        [LmStudioProviderName] = new[]
        {
            // LM Studio exposes whatever model the user has loaded; suggestions are placeholders.
            "local-model"
        },
        [OpenAiCompatibleProviderName] = new[]
        {
            // Free-form. Anything that speaks the OpenAI chat-completions schema.
            "llama-3.3-70b-versatile",
            "mixtral-8x7b-32768"
        }
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ProviderSttModelSuggestions = new Dictionary<string, IReadOnlyList<string>>
    {
        [DeepgramProviderName] = new[]
        {
            "nova-3",
            "nova-2",
            "enhanced",
            "base"
        },
        [OpenAiCompatibleProviderName] = new[]
        {
            "whisper-large-v3",
            "whisper-large-v3-turbo"
        },
        [AssemblyAiProviderName] = new[]
        {
            "universal",
            "best",
            "nano"
        },
        [ElevenLabsProviderName] = new[]
        {
            "scribe_v1"
        },
        [AzureSpeechProviderName] = new[]
        {
            // Azure Speech Short Audio uses the region URL; "model" is informational here.
            "default"
        },
        [GroqProviderName] = new[]
        {
            "whisper-large-v3",
            "whisper-large-v3-turbo"
        }
    };
    public static readonly IReadOnlyList<string> OpenAiSttModels = ["gpt-4o-mini-transcribe", "gpt-4o-transcribe"];
    /// <summary>Bekannte Chat-Completions-Modelle (Auswahl); jede andere Modell-ID kann manuell eingetragen werden.</summary>
    public static readonly IReadOnlyList<string> OpenAiLlmModels =
    [
        "gpt-5.1",
        "gpt-5.4-nano",
        "gpt-5.4-mini",
        "gpt-5-mini",
        "gpt-5-nano",
        "gpt-5.4",
        "gpt-5.4-pro",
        "gpt-5.5",
        "gpt-5.5-pro",
        "gpt-5",
        "gpt-5.2",
        "gpt-5.3",
        "gpt-4o-mini",
        "gpt-4o",
        "gpt-4.1-nano",
        "gpt-4.1-mini",
        "gpt-4.1",
        "gpt-4-turbo",
        "gpt-4",
        "gpt-3.5-turbo"
    ];

    /// <summary>Sprachen mit Whisper/OpenAI-STT-kompatiblen Codes und Anzeigenamen (alphabetisch).</summary>
    private static readonly LanguageOption[] SelectableLanguages =
    [
        new("af", "Afrikaans"),
        new("sq", "Albanisch"),
        new("am", "Amharisch"),
        new("ar", "Arabisch"),
        new("hy", "Armenisch"),
        new("as", "Assamesisch"),
        new("az", "Aserbaidschanisch"),
        new("ba", "Baschkirisch"),
        new("eu", "Baskisch"),
        new("be", "Weißrussisch"),
        new("bn", "Bengalisch"),
        new("my", "Birmanisch"),
        new("bs", "Bosnisch"),
        new("br", "Bretonisch"),
        new("bg", "Bulgarisch"),
        new("zh", "Chinesisch (Mandarin)"),
        new("da", "Dänisch"),
        new("de", "Deutsch"),
        new("en", "Englisch"),
        new("et", "Estnisch"),
        new("fo", "Färöisch"),
        new("tl", "Filipino"),
        new("fi", "Finnisch"),
        new("fr", "Französisch"),
        new("gl", "Galicisch"),
        new("ka", "Georgisch"),
        new("el", "Griechisch"),
        new("gu", "Gujarati"),
        new("ht", "Haitianisch"),
        new("ha", "Hausa"),
        new("haw", "Hawaiisch"),
        new("he", "Hebräisch"),
        new("hi", "Hindi"),
        new("id", "Indonesisch"),
        new("is", "Isländisch"),
        new("it", "Italienisch"),
        new("ja", "Japanisch"),
        new("jw", "Javanisch"),
        new("yi", "Jiddisch"),
        new("kn", "Kannada"),
        new("yue", "Kantonesisch"),
        new("kk", "Kasachisch"),
        new("ca", "Katalanisch"),
        new("km", "Khmer"),
        new("ko", "Koreanisch"),
        new("hr", "Kroatisch"),
        new("lo", "Laotisch"),
        new("la", "Lateinisch"),
        new("lv", "Lettisch"),
        new("ln", "Lingala"),
        new("lt", "Litauisch"),
        new("lb", "Luxemburgisch"),
        new("mg", "Madagassisch"),
        new("ms", "Malaiisch"),
        new("ml", "Malayalam"),
        new("mt", "Maltesisch"),
        new("mi", "Maori"),
        new("mr", "Marathi"),
        new("mk", "Mazedonisch"),
        new("mn", "Mongolisch"),
        new("ne", "Nepalesisch"),
        new("nl", "Niederländisch"),
        new("nn", "Norwegisch (Nynorsk)"),
        new("no", "Norwegisch (Bokmål)"),
        new("oc", "Okzitanisch"),
        new("ps", "Paschtu"),
        new("fa", "Persisch"),
        new("pl", "Polnisch"),
        new("pt", "Portugiesisch"),
        new("pa", "Punjabi"),
        new("ro", "Rumänisch"),
        new("ru", "Russisch"),
        new("sa", "Sanskrit"),
        new("sv", "Schwedisch"),
        new("sr", "Serbisch"),
        new("sn", "Shona"),
        new("si", "Singhalesisch"),
        new("sd", "Sindhi"),
        new("sk", "Slowakisch"),
        new("sl", "Slowenisch"),
        new("so", "Somali"),
        new("es", "Spanisch"),
        new("sw", "Suaheli"),
        new("su", "Sundanesisch"),
        new("ta", "Tamil"),
        new("tg", "Tadschikisch"),
        new("tt", "Tatarisch"),
        new("te", "Telugu"),
        new("th", "Thai"),
        new("bo", "Tibetisch"),
        new("cs", "Tschechisch"),
        new("tr", "Türkisch"),
        new("tk", "Turkmenisch"),
        new("uk", "Ukrainisch"),
        new("hu", "Ungarisch"),
        new("ur", "Urdu"),
        new("uz", "Usbekisch"),
        new("vi", "Vietnamesisch"),
        new("cy", "Walisisch"),
        new("yo", "Yoruba")
    ];

    public static readonly IReadOnlyList<LanguageOption> InputLanguages =
    [
        new(AutoLanguageCode, "automatisch erkennen"),
        ..SelectableLanguages
    ];

    public static readonly IReadOnlyList<LanguageOption> OutputLanguages =
    [
        new(SameAsInputLanguageCode, "wie Eingabe"),
        ..SelectableLanguages
    ];

    public static bool TryGetSttEndpoint(string provider, out string endpoint) =>
        TryGetSttEndpoint(provider, null, out endpoint);

    public static bool TryGetSttEndpoint(string provider, string? endpointOverride, out string endpoint)
    {
        if (provider.Equals(OpenAiProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = OpenAiSttEndpoint;
            return true;
        }
        if (provider.Equals(OpenAiCompatibleProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = string.IsNullOrWhiteSpace(endpointOverride) ? string.Empty : endpointOverride!.Trim();
            return !string.IsNullOrEmpty(endpoint);
        }
        if (provider.Equals(GroqProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = GroqSttEndpoint;
            return true;
        }
        if (provider.Equals(DeepgramProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = DeepgramListenEndpoint;
            return true;
        }
        if (provider.Equals(AssemblyAiProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = AssemblyAiTranscriptEndpoint;
            return true;
        }
        if (provider.Equals(ElevenLabsProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = ElevenLabsScribeEndpoint;
            return true;
        }
        if (provider.Equals(AzureSpeechProviderName, StringComparison.OrdinalIgnoreCase))
        {
            // Azure Speech requires a region-specific URL — user must supply it via override.
            endpoint = string.IsNullOrWhiteSpace(endpointOverride) ? string.Empty : endpointOverride!.Trim();
            return !string.IsNullOrEmpty(endpoint);
        }
        endpoint = string.Empty;
        return false;
    }

    public static bool TryGetLlmEndpoint(string provider, out string endpoint) =>
        TryGetLlmEndpoint(provider, null, null, out endpoint);

    public static bool TryGetLlmEndpoint(string provider, string? endpointOverride, string? model, out string endpoint)
    {
        if (provider.Equals(OpenAiProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = OpenAiLlmEndpoint;
            return true;
        }
        if (provider.Equals(AnthropicProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = AnthropicMessagesEndpoint;
            return true;
        }
        if (provider.Equals(GoogleGeminiProviderName, StringComparison.OrdinalIgnoreCase))
        {
            // Gemini puts the model in the URL: /models/{model}:generateContent.
            var safeModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.5-flash" : model!.Trim();
            endpoint = $"{GeminiGenerateContentBase}/{safeModel}:generateContent";
            return true;
        }
        if (provider.Equals(OpenAiCompatibleProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = string.IsNullOrWhiteSpace(endpointOverride) ? string.Empty : endpointOverride!.Trim();
            return !string.IsNullOrEmpty(endpoint);
        }
        if (provider.Equals(GroqProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = GroqLlmEndpoint;
            return true;
        }
        if (provider.Equals(DeepSeekProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = DeepSeekLlmEndpoint;
            return true;
        }
        if (provider.Equals(XaiProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = XaiLlmEndpoint;
            return true;
        }
        if (provider.Equals(OllamaProviderName, StringComparison.OrdinalIgnoreCase))
        {
            // Local: allow user to override host/port via endpointOverride; otherwise default localhost.
            endpoint = string.IsNullOrWhiteSpace(endpointOverride) ? OllamaDefaultLlmEndpoint : endpointOverride!.Trim();
            return true;
        }
        if (provider.Equals(LmStudioProviderName, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = string.IsNullOrWhiteSpace(endpointOverride) ? LmStudioDefaultLlmEndpoint : endpointOverride!.Trim();
            return true;
        }
        endpoint = string.Empty;
        return false;
    }

    public static bool IsAutoLanguage(string languageCode) =>
        string.IsNullOrWhiteSpace(languageCode) || languageCode.Equals(AutoLanguageCode, StringComparison.OrdinalIgnoreCase);

    public static string LanguageName(string languageCode)
    {
        var option = InputLanguages.Concat(OutputLanguages)
            .FirstOrDefault(language => language.Code.Equals(languageCode, StringComparison.OrdinalIgnoreCase));
        return option?.Name ?? languageCode;
    }
}

public interface ISettingsService
{
    string SettingsPath { get; }
    string DataDirectory { get; }
    string LogDirectory { get; }
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
    ReadinessReport Validate(AppSettings settings);
}

public interface ISecretProtector
{
    string Protect(string secret);
    string Unprotect(string protectedSecret);
}

public interface IHotkeyService : IAsyncDisposable
{
    event EventHandler<HotkeyPressedEventArgs>? HotkeyDown;
    event EventHandler<HotkeyPressedEventArgs>? HotkeyUp;
    bool IsPaused { get; }
    Task<IReadOnlyList<ValidationIssue>> RegisterAsync(AppSettings settings, CancellationToken cancellationToken = default);
    void Pause();
    void Resume();
}

public sealed class HotkeyPressedEventArgs(string assistantId) : EventArgs
{
    public string AssistantId { get; } = assistantId;
}

public interface IAudioRecorder
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task<AudioBuffer> StopAsync(CancellationToken cancellationToken = default);
    Task AbortAsync(CancellationToken cancellationToken = default);
}

public interface IAudioDeviceService
{
    IReadOnlyList<AudioInputDevice> GetInputDevices();
}

public interface ISttService
{
    Task<string> TranscribeAsync(SttRequest request, CancellationToken cancellationToken = default);
}

public interface ILlmService
{
    Task<string> ProcessAsync(LlmRequest request, CancellationToken cancellationToken = default);
}

public interface IInputInjector
{
    Task InsertTextAsync(string text, InsertMethod method, bool restoreClipboard, CancellationToken cancellationToken = default);
}

public interface ITrayStatusService
{
    TrayStatus CurrentStatus { get; }
    string Message { get; }
    event EventHandler<TrayStatusChangedEventArgs>? StatusChanged;
    void SetStatus(TrayStatus status, string message);
}

public sealed record TrayStatusChangedEventArgs(TrayStatus Status, string Message);

/// <summary>Letzter Verarbeitungsfehler für die Diagnose-Ansicht (thread-sicher, kurz gehalten).</summary>
public interface IProcessingFailureLog
{
    void Record(string headline, Exception? exception = null);

    void Clear();

    string? LastEntry { get; }
}

public sealed class InMemoryProcessingFailureLog : IProcessingFailureLog
{
    private const int MaxChars = 1200;
    private readonly object _gate = new();
    private string? _last;

    public string? LastEntry
    {
        get
        {
            lock (_gate)
            {
                return _last;
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _last = null;
        }
    }

    public void Record(string headline, Exception? exception = null)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).AppendLine("]");
        sb.AppendLine(headline);
        if (exception is not null)
        {
            sb.Append(FormatExceptionForDiagnostics(exception));
        }

        var text = sb.ToString().TrimEnd();
        if (text.Length > MaxChars)
        {
            text = text[..MaxChars] + "…";
        }

        lock (_gate)
        {
            _last = text;
        }
    }

    private static string FormatExceptionForDiagnostics(Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine(UserFacingExceptionSummary(ex));

        var details = new List<string>();
        var index = 0;
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var message = string.IsNullOrWhiteSpace(e.Message) ? "(keine Message)" : e.Message.Trim();
            details.Add($"{e.GetType().Name}: {message}");
            index++;
        }

        if (details.Count > 0)
        {
            sb.Append("Details: ").AppendLine(string.Join(" | ", details.Take(3)));
        }

        return sb.ToString();
    }

    private static string UserFacingExceptionSummary(Exception ex)
    {
        if (ex is OperationCanceledException || ContainsException<OperationCanceledException>(ex))
        {
            return "Die Anfrage wurde abgebrochen oder hat das konfigurierte Zeitlimit erreicht. Bitte Netzwerk, Anbieter-Erreichbarkeit und das Zeitlimit in den Einstellungen prüfen.";
        }

        if (ContainsException<HttpRequestException>(ex) || ContainsException<IOException>(ex))
        {
            return "Die Anfrage konnte wegen eines Netzwerk- oder Transportfehlers nicht abgeschlossen werden. Bitte Anbieter, Netzwerk und Endpoint prüfen.";
        }

        return "Die Verarbeitung konnte nicht abgeschlossen werden. Die Details unten nennen den technischen Fehler.";
    }

    private static bool ContainsException<T>(Exception ex) where T : Exception
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is T)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Eintrag im Verarbeitungsverlauf für die Diagnose-Ansicht.</summary>
public sealed record ProcessingTimings(
    TimeSpan Total,
    TimeSpan AudioDuration,
    TimeSpan RecorderStop,
    TimeSpan Transcription,
    TimeSpan Llm,
    TimeSpan Insert);

public sealed record ProcessingHistoryEntry(
    DateTimeOffset Timestamp,
    string AssistantName,
    AssistantMode AssistantType,
    string SystemPrompt,
    string ModePrompt,
    string Transcript,
    string FinalText,
    InsertMethod InsertMethod,
    bool UsedClipboardFallback,
    ProcessingTimings? Timings = null);

/// <summary>Ringpuffer der letzten erfolgreichen Verarbeitungen (thread-sicher).</summary>
public interface IProcessingHistoryLog
{
    void Record(ProcessingHistoryEntry entry);
    void Clear();
    IReadOnlyList<ProcessingHistoryEntry> Entries { get; }
}

public sealed class InMemoryProcessingHistoryLog : IProcessingHistoryLog
{
    public const int MaxEntries = 5;
    private readonly object _gate = new();
    private readonly LinkedList<ProcessingHistoryEntry> _entries = new();

    public IReadOnlyList<ProcessingHistoryEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    public void Record(ProcessingHistoryEntry entry)
    {
        lock (_gate)
        {
            _entries.AddFirst(entry);
            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveLast();
            }
        }
    }
}

public sealed class NullProcessingHistoryLog : IProcessingHistoryLog
{
    public void Record(ProcessingHistoryEntry entry) { }
    public void Clear() { }
    public IReadOnlyList<ProcessingHistoryEntry> Entries => Array.Empty<ProcessingHistoryEntry>();
}

public sealed class NullProcessingFailureLog : IProcessingFailureLog
{
    public void Record(string headline, Exception? exception = null)
    {
    }

    public void Clear()
    {
    }

    public string? LastEntry => null;
}

public interface IAutostartService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

public interface IFeedbackSoundService
{
    Task PlayRecordingStartAsync(CancellationToken cancellationToken = default);
    Task PlayRecordingStopAsync(CancellationToken cancellationToken = default);
}

/// <summary>Liest den aktuellen Zwischenablage-Inhalt (Unicode-Text) für Modi, die einen Quelltext benötigen.</summary>
public interface IClipboardSourceCapture
{
    string? TryGetText();
}

public static class HotkeyParser
{
    private static readonly HashSet<string> Modifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ctrl", "Control", "Alt", "Shift", "Win", "Windows"
    };

    public static bool TryParse(string? text, out HotkeyGesture gesture, out string error)
    {
        gesture = new HotkeyGesture(false, false, false, false, string.Empty);
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Tastenkürzel darf nicht leer sein.";
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            error = L.S("hotkey.validation.need_modifier_and_key");
            return false;
        }

        var control = false;
        var alt = false;
        var shift = false;
        var windows = false;
        string? key = null;

        foreach (var part in parts)
        {
            if (Modifiers.Contains(part))
            {
                control |= part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase);
                alt |= part.Equals("Alt", StringComparison.OrdinalIgnoreCase);
                shift |= part.Equals("Shift", StringComparison.OrdinalIgnoreCase);
                windows |= part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (key is not null)
            {
                error = "Nur eine Haupttaste ist erlaubt.";
                return false;
            }

            key = NormalizeKey(part);
        }

        if (!control && !alt && !shift && !windows)
        {
            error = L.S("hotkey.validation.need_modifier");
            return false;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            error = L.S("hotkey.validation.need_main_key");
            return false;
        }

        gesture = new HotkeyGesture(control, alt, shift, windows, key);
        return true;
    }

    private static string NormalizeKey(string key)
    {
        key = key.Trim();
        return key.Length == 1 ? key.ToUpperInvariant() : key;
    }
}

public sealed class PromptService(IAppProfile profile)
{
    public string GetPrompt(AssistantInstance assistant) =>
        !string.IsNullOrWhiteSpace(assistant.Prompt)
            ? assistant.Prompt
            : profile.Modes.FirstOrDefault(definition => definition.Mode == assistant.Type)?.DefaultPrompt ?? string.Empty;
}

public sealed class SpeechPipeline(
    IAppProfile profile,
    IAudioRecorder audioRecorder,
    ISttService sttService,
    ILlmService llmService,
    IInputInjector inputInjector,
    ISettingsService settingsService,
    ISecretProtector secretProtector,
    IHotkeyService hotkeyService,
    ITrayStatusService trayStatusService,
    IProcessingFailureLog failureLog,
    IProcessingHistoryLog historyLog)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly PromptService _promptService = new(profile);

    private PipelineResult Fail(string message, Exception? ex = null, string? transcript = null, string? finalText = null)
    {
        failureLog.Record(message, ex);
        return PipelineResult.Failed(message, ex, transcript, finalText);
    }

    public Task<PipelineResult> RunAsync(string assistantId, string? sourceText, CancellationToken cancellationToken = default) =>
        RunAsync(assistantId, sourceText, audio: null, cancellationToken);

    public async Task<PipelineResult> RunAsync(string assistantId, string? sourceText, AudioBuffer? audio, CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            trayStatusService.SetStatus(TrayStatus.Processing, "Text wird bereits verarbeitet...");
            return Fail(L.S("pipeline.error.in_progress"));
        }

        var totalStopwatch = Stopwatch.StartNew();
        var recorderStopDuration = TimeSpan.Zero;
        var transcriptionDuration = TimeSpan.Zero;
        var llmDuration = TimeSpan.Zero;
        var insertDuration = TimeSpan.Zero;
        var currentPhase = "Vorbereitung";
        var phaseStopwatch = Stopwatch.StartNew();
        var processingTimeoutSeconds = 0;
        var audioDuration = TimeSpan.Zero;
        var sttContext = "STT: noch nicht gestartet";
        var llmContext = "KI: noch nicht gestartet";

        void BeginPhase(string phase)
        {
            currentPhase = phase;
            phaseStopwatch.Restart();
        }

        string FailureContext() =>
            string.Join(
                Environment.NewLine,
                $"Phase: {currentPhase} ({FormatDuration(phaseStopwatch.Elapsed)} in dieser Phase, gesamt {FormatDuration(totalStopwatch.Elapsed)})",
                processingTimeoutSeconds > 0 ? $"Zeitlimit: {processingTimeoutSeconds}s" : "Zeitlimit: noch nicht geladen",
                audioDuration > TimeSpan.Zero ? $"Aufnahmelänge: {FormatDuration(audioDuration)}" : "Aufnahmelänge: noch nicht verfügbar",
                $"Bisherige Zeiten: Recorder-Stopp {FormatDuration(recorderStopDuration)} | Transkription {FormatDuration(transcriptionDuration)} | KI {FormatDuration(llmDuration)} | Einfügen {FormatDuration(insertDuration)}",
                sttContext,
                llmContext);

        try
        {
            BeginPhase("Einstellungen laden");
            var settings = await settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
            processingTimeoutSeconds = settings.ProcessingTimeoutSeconds;
            var readiness = settingsService.Validate(settings);
            if (!readiness.IsReady)
            {
                trayStatusService.SetStatus(TrayStatus.ConfigurationRequired, L.S("status.setup_required.long"));
                return Fail(L.S("pipeline.error.setup_required"));
            }

            var assistant = settings.Assistants.FirstOrDefault(a => string.Equals(a.Id, assistantId, StringComparison.Ordinal));
            if (assistant is null)
            {
                trayStatusService.SetStatus(TrayStatus.Error, L.S("pipeline.error.assistant_not_found_user"));
                return Fail(L.F("pipeline.error.assistant_not_found_log", assistantId));
            }

            var effectiveInputLanguage = PromptComposition.EffectiveInputLanguage(settings, assistant);
            var effectiveOutputLanguage = PromptComposition.EffectiveOutputLanguage(settings, assistant);
            trayStatusService.SetStatus(TrayStatus.Processing, "Text wird verarbeitet...");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.ProcessingTimeoutSeconds));

            if (audio is null)
            {
                BeginPhase("Aufnahme stoppen");
                var recorderStopwatch = Stopwatch.StartNew();
                audio = await audioRecorder.StopAsync(timeout.Token).ConfigureAwait(false);
                recorderStopDuration = recorderStopwatch.Elapsed;
            }
            audioDuration = audio.Duration;
            if (audio.IsEmpty || audio.Duration < TimeSpan.FromMilliseconds(settings.MinimumRecordingMilliseconds))
            {
                trayStatusService.SetStatus(TrayStatus.Idle, L.S("pipeline.error.no_speech_user"));
                return Fail(L.S("pipeline.error.no_speech_log"));
            }

            var sttKey = string.IsNullOrWhiteSpace(settings.SttApiKeyEncrypted)
                ? string.Empty
                : secretProtector.Unprotect(settings.SttApiKeyEncrypted);
            Defaults.TryGetSttEndpoint(settings.SttProvider, settings.SttEndpointOverride, out var sttEndpoint);
            sttContext = $"STT: {settings.SttProvider} / {settings.SttModel} / {sttEndpoint}";

            var sttMaxAttempts = 1 + Math.Max(0, settings.TranscriptionRetriesOnFailure);
            string transcript = null!;
            var transcriptionStopwatch = Stopwatch.StartNew();
            BeginPhase("Transkription");
            trayStatusService.SetStatus(TrayStatus.Processing, "Transkription läuft …");
            for (var attempt = 0; attempt < sttMaxAttempts; attempt++)
            {
                try
                {
                    transcript = await sttService.TranscribeAsync(
                        new SttRequest(audio, settings.SttProvider, sttEndpoint, settings.SttModel, effectiveInputLanguage, sttKey),
                        timeout.Token).ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (attempt + 1 == sttMaxAttempts)
                    {
                        trayStatusService.SetStatus(TrayStatus.Error, "Transkription fehlgeschlagen. Bitte API-Schlüssel, Modell und Netzwerk prüfen.");
                        return Fail($"Transkription fehlgeschlagen.{Environment.NewLine}{FailureContext()}", ex);
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), timeout.Token).ConfigureAwait(false);
                }
            }
            transcriptionDuration = transcriptionStopwatch.Elapsed;

            // Whisper liefert bei Stille/Rauschen oft sehr kurze "Halluzinationen" wie ".", "Ja" oder ein einzelnes Zeichen.
            // Diese Fragmente an die KI weiterzugeben führt zu Entschuldigungs-Antworten ("Bitte stelle den Text bereit ...")
            // — also lieber sauber abbrechen, statt eine LLM-Anfrage zu verschwenden und dann sinnlosen Text einzufügen.
            transcript = (transcript ?? string.Empty).Trim();
            if (transcript.Length < 2)
            {
                trayStatusService.SetStatus(TrayStatus.Idle, L.S("pipeline.error.no_text_user"));
                return Fail("Transkription lieferte keinen brauchbaren Text (leeres oder zu kurzes Ergebnis).");
            }

            var llmKey = string.IsNullOrWhiteSpace(settings.LlmApiKeyEncrypted)
                ? string.Empty
                : secretProtector.Unprotect(settings.LlmApiKeyEncrypted);
            Defaults.TryGetLlmEndpoint(settings.LlmProvider, settings.LlmEndpointOverride, settings.LlmModel, out var llmEndpoint);
            llmContext = $"KI: {settings.LlmProvider} / {settings.LlmModel} / {llmEndpoint}";
            var baseSystemPrompt = PromptComposition.EffectiveBaseSystemPrompt(profile, assistant);
            var policyBlock = PromptComposition.BuildPolicyBlock(profile, assistant, effectiveInputLanguage, effectiveOutputLanguage);
            // Append the glossary block (proper names/terms) from the assistant's enabled spelling sets.
            var glossaryBlock = PromptComposition.BuildGlossaryBlock(
                SpellingPostProcessor.ResolveActiveTerms(settings, assistant));
            if (!string.IsNullOrWhiteSpace(glossaryBlock))
            {
                policyBlock = string.IsNullOrWhiteSpace(policyBlock)
                    ? glossaryBlock
                    : policyBlock + Environment.NewLine + Environment.NewLine + glossaryBlock;
            }
            var systemPrompt = PromptComposition.BuildSystemPrompt(baseSystemPrompt, effectiveInputLanguage, policyBlock);
            var modePrompt = BuildModePrompt(assistant, sourceText);
            var llmRequest = new LlmRequest(transcript, assistant.Type, settings.LlmProvider, llmEndpoint, settings.LlmModel, llmKey, systemPrompt, modePrompt, sourceText);

            string finalText = string.Empty;
            var llmMaxAttempts = 1 + Math.Max(0, settings.LlmRetriesOnFailure);
            var llmStopwatch = Stopwatch.StartNew();
            BeginPhase("KI-Verarbeitung");
            trayStatusService.SetStatus(TrayStatus.Processing, "KI-Verarbeitung läuft …");
            for (var attempt = 0; attempt < llmMaxAttempts; attempt++)
            {
                try
                {
                    finalText = await llmService.ProcessAsync(llmRequest, timeout.Token).ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (attempt + 1 == llmMaxAttempts)
                    {
                        failureLog.Record($"KI-Verarbeitung (Chat) fehlgeschlagen; es wird das Transkript eingefügt.{Environment.NewLine}{FailureContext()}", ex);
                        finalText = transcript;
                        trayStatusService.SetStatus(TrayStatus.Error, "KI-Verarbeitung fehlgeschlagen. Das Transkript wird eingefügt.");
                        break;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), timeout.Token).ConfigureAwait(false);
                }
            }
            llmDuration = llmStopwatch.Elapsed;

            if (string.IsNullOrWhiteSpace(finalText))
            {
                trayStatusService.SetStatus(TrayStatus.Idle, L.S("pipeline.error.no_text_user"));
                return Fail(L.S("pipeline.error.no_final_text"), transcript: transcript);
            }

            finalText = SpellingPostProcessor.Apply(finalText, SpellingPostProcessor.ResolveActiveReplacements(settings, assistant));
            finalText = TextNormalizer.Normalize(finalText);

            var insertUsedClipboardFallback = false;
            try
            {
                BeginPhase("Einfügen");
                trayStatusService.SetStatus(TrayStatus.Processing, "Text wird eingefügt …");
                var insertStopwatch = Stopwatch.StartNew();
                insertUsedClipboardFallback = await InsertFinalTextAsync(
                    finalText,
                    settings.InsertMethod,
                    settings.RestoreClipboard,
                    settings.ClipboardInsertRetriesOnFailure,
                    timeout.Token).ConfigureAwait(false);
                insertDuration = insertStopwatch.Elapsed;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                trayStatusService.SetStatus(TrayStatus.Error, L.S("pipeline.error.insert_failed_user"));
                return Fail($"{L.F("pipeline.error.insert_failed_log", settings.InsertMethod)}{Environment.NewLine}{FailureContext()}", ex, transcript: transcript, finalText: finalText);
            }

            failureLog.Clear();
            if (insertUsedClipboardFallback)
            {
                failureLog.Record(
                    "Text eingefügt. Die Zwischenablage ließ sich nicht nutzen; es wurde automatisch per Tastatur eingegeben (Fallback).");
            }

            if (settings.KeepProcessingHistory)
            {
                var assistantLabel = string.IsNullOrWhiteSpace(assistant.Name) ? assistant.Type.ToString() : assistant.Name;
                totalStopwatch.Stop();
                historyLog.Record(new ProcessingHistoryEntry(
                    DateTimeOffset.Now,
                    assistantLabel,
                    assistant.Type,
                    systemPrompt,
                    modePrompt,
                    transcript,
                    finalText,
                    settings.InsertMethod,
                    insertUsedClipboardFallback,
                    new ProcessingTimings(
                        totalStopwatch.Elapsed,
                        audio.Duration,
                        recorderStopDuration,
                        transcriptionDuration,
                        llmDuration,
                        insertDuration)));
            }
            else
            {
                historyLog.Clear();
            }

            trayStatusService.SetStatus(
                TrayStatus.Success,
                insertUsedClipboardFallback
                    ? "Text eingefügt (Fallback: direktes Tippen, da die Zwischenablage blockiert war)."
                    : "Text eingefügt.");
            _ = ResetTransientStatusAsync(settingsService, hotkeyService, trayStatusService, TimeSpan.FromSeconds(2), cancellationToken);
            return PipelineResult.Ok("Text eingefügt.", transcript, finalText);
        }
        catch (OperationCanceledException ex)
        {
            trayStatusService.SetStatus(TrayStatus.Error, "Zeitüberschreitung. Es wurde nichts eingefügt.");
            return Fail($"Zeitüberschreitung bei der Verarbeitung.{Environment.NewLine}{FailureContext()}", ex);
        }
        catch (Exception ex)
        {
            trayStatusService.SetStatus(TrayStatus.Error, "Unerwarteter Fehler. Bitte Diagnose prüfen.");
            return Fail($"Unerwarteter Pipeline-Fehler.{Environment.NewLine}{FailureContext()}", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Liefert <see langword="true"/>, wenn nach fehlgeschlagenen Zwischenablage-Versuchen per <see cref="InsertMethod.SendInput"/> eingefügt wurde.
    /// Bei <see cref="InsertMethod.Clipboard"/> werden bis zu <paramref name="clipboardInsertRetriesOnFailure"/> zusätzliche Versuche nach einem Fehler unternommen, danach Fallback auf SendInput.
    /// Ob die Zielanwendung Einfügen wirklich übernommen hat, lässt sich ohne UI-Prüfung nicht erkennen.
    /// </summary>
    private async Task<bool> InsertFinalTextAsync(
        string finalText,
        InsertMethod insertMethod,
        bool restoreClipboard,
        int clipboardInsertRetriesOnFailure,
        CancellationToken cancellationToken)
    {
        if (insertMethod != InsertMethod.Clipboard)
        {
            await inputInjector.InsertTextAsync(finalText, insertMethod, restoreClipboard, cancellationToken).ConfigureAwait(false);
            return false;
        }

        var maxAttempts = 1 + Math.Max(0, clipboardInsertRetriesOnFailure);
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await inputInjector.InsertTextAsync(finalText, InsertMethod.Clipboard, restoreClipboard, cancellationToken).ConfigureAwait(false);
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                if (attempt + 1 == maxAttempts)
                {
                    trayStatusService.SetStatus(TrayStatus.Processing, "Zwischenablage nicht nutzbar – Text wird eingegeben …");
                    await inputInjector.InsertTextAsync(finalText, InsertMethod.SendInput, restoreClipboard: false, cancellationToken).ConfigureAwait(false);
                    return true;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), cancellationToken).ConfigureAwait(true);
            }
        }

        throw new InvalidOperationException(L.S("pipeline.error.clipboard_no_retries"));
    }

    private string BuildModePrompt(AssistantInstance assistant, string? sourceText)
    {
        var prompt = _promptService.GetPrompt(assistant);
        var sourceBlock = !string.IsNullOrWhiteSpace(sourceText)
            ? $"Quelltext (Bezug für die Antwort, nicht in die Ausgabe übernehmen):{Environment.NewLine}\"\"\"{Environment.NewLine}{sourceText.Trim()}{Environment.NewLine}\"\"\""
            : string.Empty;
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { prompt, sourceBlock }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return duration.TotalSeconds >= 1
            ? $"{duration.TotalSeconds:0.0}s"
            : $"{duration.TotalMilliseconds:0}ms";
    }

    private static async Task ResetTransientStatusAsync(
        ISettingsService settingsService,
        IHotkeyService hotkeyService,
        ITrayStatusService trayStatusService,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            if (trayStatusService.CurrentStatus != TrayStatus.Success)
            {
                return;
            }

            var settings = await settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
            var readiness = settingsService.Validate(settings);
            if (hotkeyService.IsPaused)
            {
                trayStatusService.SetStatus(TrayStatus.Paused, L.S("status.inactive.long"));
            }
            else if (readiness.IsReady)
            {
                trayStatusService.SetStatus(TrayStatus.Idle, L.S("status.ready.hint"));
            }
            else
            {
                trayStatusService.SetStatus(TrayStatus.ConfigurationRequired, L.S("status.setup_required.long"));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}

public sealed class InMemoryTrayStatusService : ITrayStatusService
{
    public TrayStatus CurrentStatus { get; private set; } = TrayStatus.Idle;
    public string Message { get; private set; } = L.S("status.ready.hint");
    public event EventHandler<TrayStatusChangedEventArgs>? StatusChanged;

    public void SetStatus(TrayStatus status, string message)
    {
        CurrentStatus = status;
        Message = message;
        StatusChanged?.Invoke(this, new TrayStatusChangedEventArgs(status, message));
    }
}
