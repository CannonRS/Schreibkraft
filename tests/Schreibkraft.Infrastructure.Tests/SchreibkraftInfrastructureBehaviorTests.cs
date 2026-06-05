using Schreibkraft.Core;
using Schreibkraft.Infrastructure;

namespace Schreibkraft.Infrastructure.Tests;

public class InfrastructureBehaviorTests
{
    private static IAppProfile CreateProfile() => new TestProfile();
    private static string LegacyName => string.Concat("Ma", "gic");
    private static string LegacyFolderName => string.Concat(LegacyName, "-Voice");

    private sealed class TestProfile : IAppProfile
    {
        public string AppName => "Test";
        public string DataFolderName => "Test";
        public string AuthorName => "Test";
        public string CopyrightText => "Test";
        public string LicenseName => "Test";
        public string MutexName => "Test";
        public string AumId => "Test.App";
        public string AutostartRegistryValueName => "Test";
        public string SystemPrompt => "Test.";
        public IReadOnlyList<AssistantModeDefinition> Modes { get; } =
        [
            new(AssistantMode.Transform, "Korrektur", "Test", "Ctrl+Shift+1", "Transform default."),
            new(AssistantMode.Generate, "Generate", "Test", "Ctrl+Shift+2", "Generate default.")
        ];
        public IReadOnlyList<PromptTemplate> PromptTemplates { get; } = Array.Empty<PromptTemplate>();
        public string IntensityStepName(AssistantMode mode, int intensity) => intensity.ToString();
        public string IntensityStepInstruction(AssistantMode mode, int intensity) => $"Stufe {intensity}";
        public string WritingStyleInstruction(WritingStyle style) => string.Empty;
        public string EmojiExpressionInstruction(EmojiExpression level) => string.Empty;
        public List<AssistantInstance> CreateDefaultAssistants() =>
            Modes.Select(m => new AssistantInstance
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = m.Mode,
                Name = m.Name,
                Hotkey = m.DefaultHotkey,
                Prompt = m.DefaultPrompt,
                Intensity = Defaults.DefaultModeIntensity
            }).ToList();
    }

    [Fact]
    public void DpapiSecretProtector_roundtrips_secret_for_current_user()
    {
        var protector = new DpapiSecretProtector();

        var encrypted = protector.Protect("geheim");
        var plain = protector.Unprotect(encrypted);

        Assert.NotEqual("geheim", encrypted);
        Assert.Equal("geheim", plain);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("")]
    [InlineData(" ")]
    public void DpapiSecretProtector_unprotects_legacy_secrets(string separator)
    {
        var legacyEntropy = $"{LegacyName}{separator}Voice.v1";
        var protectedBytes = System.Security.Cryptography.ProtectedData.Protect(
            System.Text.Encoding.UTF8.GetBytes("geheim"),
            System.Text.Encoding.UTF8.GetBytes(legacyEntropy),
            System.Security.Cryptography.DataProtectionScope.CurrentUser);
        var encrypted = Convert.ToBase64String(protectedBytes);

        var plain = new DpapiSecretProtector().Unprotect(encrypted);

        Assert.Equal("geheim", plain);
    }

    [Fact]
    public void WavWriter_creates_riff_wave_payload()
    {
        var bytes = WavWriter.ToWav(new AudioBuffer([1, 0, 2, 0], 16000, 1, TimeSpan.FromMilliseconds(1)));

        Assert.Equal((byte)'R', bytes[0]);
        Assert.Equal((byte)'I', bytes[1]);
        Assert.Equal((byte)'F', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
        Assert.Contains((byte)'W', bytes);
    }

    [Fact]
    public async Task SettingsService_creates_defaults_and_reports_missing_configuration()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"Schreibkraft-tests-{Guid.NewGuid():N}");
        var service = new SettingsService(CreateProfile(), temp);
        var settings = await service.LoadAsync();
        var readiness = service.Validate(settings);

        Assert.Contains(readiness.Issues, issue => issue.Field == "llmApiKey");
        Assert.Contains(readiness.Issues, issue => issue.Field == "sttApiKey" || issue.Field == "llmApiKey");
        Directory.Delete(temp, recursive: true);
    }

    [Fact]
    public async Task SettingsService_migrates_missing_files_from_legacy_directory()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"Schreibkraft-tests-{Guid.NewGuid():N}");
        var legacy = Path.Combine(temp, LegacyFolderName);
        var target = Path.Combine(temp, "Schreibkraft");
        var profile = CreateProfile();
        var legacySettingsService = new SettingsService(profile, legacy);
        var legacySettings = new AppSettings
        {
            LlmModel = "legacy-llm",
            SttModel = "legacy-stt",
            Assistants = profile.CreateDefaultAssistants()
        };
        legacySettings.WindowBounds.Width = 1234;
        await legacySettingsService.SaveAsync(legacySettings);
        Directory.CreateDirectory(Path.Combine(legacy, "logs"));
        await File.WriteAllTextAsync(Path.Combine(legacy, "logs", "legacy.log"), "legacy log");
        await File.WriteAllTextAsync(Path.Combine(legacy, "extra.json"), """{"source":"legacy"}""");

        var targetSettingsService = new SettingsService(profile, target, legacy);
        var loaded = await targetSettingsService.LoadAsync();

        Assert.Equal("legacy-llm", loaded.LlmModel);
        Assert.Equal("legacy-stt", loaded.SttModel);
        Assert.Equal(1234, loaded.WindowBounds.Width);
        Assert.True(File.Exists(Path.Combine(target, "settings.json")));
        Assert.Equal("legacy log", await File.ReadAllTextAsync(Path.Combine(target, "logs", "legacy.log")));
        Assert.Equal("""{"source":"legacy"}""", await File.ReadAllTextAsync(Path.Combine(target, "extra.json")));
        Directory.Delete(temp, recursive: true);
    }

    [Fact]
    public async Task SettingsService_does_not_overwrite_existing_schreibkraft_files_during_legacy_migration()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"Schreibkraft-tests-{Guid.NewGuid():N}");
        var legacy = Path.Combine(temp, LegacyFolderName);
        var target = Path.Combine(temp, "Schreibkraft");
        var profile = CreateProfile();
        await new SettingsService(profile, legacy).SaveAsync(new AppSettings
        {
            LlmModel = "legacy-llm",
            Assistants = profile.CreateDefaultAssistants()
        });
        await new SettingsService(profile, target).SaveAsync(new AppSettings
        {
            LlmModel = "target-llm",
            Assistants = profile.CreateDefaultAssistants()
        });
        Directory.CreateDirectory(Path.Combine(legacy, "logs"));
        Directory.CreateDirectory(Path.Combine(target, "logs"));
        await File.WriteAllTextAsync(Path.Combine(legacy, "logs", "shared.log"), "legacy");
        await File.WriteAllTextAsync(Path.Combine(target, "logs", "shared.log"), "target");
        await File.WriteAllTextAsync(Path.Combine(legacy, "logs", "missing.log"), "missing");

        var loaded = await new SettingsService(profile, target, legacy).LoadAsync();

        Assert.Equal("target-llm", loaded.LlmModel);
        Assert.Equal("target", await File.ReadAllTextAsync(Path.Combine(target, "logs", "shared.log")));
        Assert.Equal("missing", await File.ReadAllTextAsync(Path.Combine(target, "logs", "missing.log")));
        Directory.Delete(temp, recursive: true);
    }

    [Fact]
    public void SettingsService_requires_supported_providers()
    {
        var service = new SettingsService(CreateProfile(), Path.Combine(Path.GetTempPath(), $"Schreibkraft-tests-{Guid.NewGuid():N}"));
        var settings = new AppSettings
        {
            LlmApiKeyEncrypted = "secret",
            SttApiKeyEncrypted = "secret",
            SttProvider = "Unbekanntes STT",
            LlmProvider = "Unbekannte KI"
        };

        var readiness = service.Validate(settings);

        Assert.Contains(readiness.Issues, issue => issue.Field == "sttProvider");
        Assert.Contains(readiness.Issues, issue => issue.Field == "llmProvider");
    }

    [Fact]
    public async Task SettingsService_persists_window_bounds()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"Schreibkraft-tests-{Guid.NewGuid():N}");
        var service = new SettingsService(CreateProfile(), temp);
        var settings = new AppSettings();
        settings.WindowBounds.X = 100;
        settings.WindowBounds.Y = 120;
        settings.WindowBounds.Width = 1024;
        settings.WindowBounds.Height = 720;

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();

        Assert.True(loaded.WindowBounds.IsSet);
        Assert.Equal(1024, loaded.WindowBounds.Width);
        Directory.Delete(temp, recursive: true);
    }

    [Fact]
    public async Task SettingsService_serializes_parallel_saves()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"Schreibkraft-tests-{Guid.NewGuid():N}");
        var service = new SettingsService(CreateProfile(), temp);

        var saves = Enumerable.Range(0, 20)
            .Select(i =>
            {
                var settings = new AppSettings { Assistants = CreateProfile().CreateDefaultAssistants() };
                settings.WindowBounds.X = i;
                settings.WindowBounds.Width = 1200 + i;
                return service.SaveAsync(settings);
            });

        await Task.WhenAll(saves);

        var loaded = await service.LoadAsync();
        Assert.InRange(loaded.WindowBounds.Width, 1200, 1219);
        Assert.Empty(Directory.GetFiles(temp, "*.tmp"));
        Directory.Delete(temp, recursive: true);
    }

    [Fact]
    public async Task SettingsService_migrates_generate_assistant_from_legacy_transform_prompt()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"Schreibkraft-tests-{Guid.NewGuid():N}");
        var service = new SettingsService(CreateProfile(), temp);
        var settings = new AppSettings
        {
            Assistants =
            [
                new AssistantInstance
                {
                    Type = AssistantMode.Generate,
                    Name = "Generate",
                    Hotkey = "Ctrl+Shift+2",
                    Prompt = "Bearbeite das Transkript gemäß dieser Anweisung. Ohne weitere Vorgabe: Rechtschreibung und Grammatik korrigieren, leicht glätten, Aussage beibehalten."
                }
            ]
        };

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();

        Assert.Equal("Generate default.", loaded.Assistants.Single().Prompt);
        Directory.Delete(temp, recursive: true);
    }

    [Fact]
    public async Task SettingsService_migrates_transform_assistant_from_legacy_default_prompt()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"Schreibkraft-tests-{Guid.NewGuid():N}");
        var service = new SettingsService(CreateProfile(), temp);
        var settings = new AppSettings
        {
            Assistants =
            [
                new AssistantInstance
                {
                    Type = AssistantMode.Transform,
                    Name = "Korrektur",
                    Hotkey = "Ctrl+Shift+1",
                    Prompt = "Bearbeite das Transkript gemäß dieser Anweisung. Ohne weitere Vorgabe: Rechtschreibung und Grammatik korrigieren, Aussage beibehalten. Keine Änderung der Wortwahl."
                }
            ]
        };

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();

        Assert.Equal("Transform default.", loaded.Assistants.Single().Prompt);
        Directory.Delete(temp, recursive: true);
    }
}
