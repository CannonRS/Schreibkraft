using Schreibkraft.Core;
using Schreibkraft.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Schreibkraft;

/// <summary>
/// Zentrale DI-Konfiguration. Liegt analog zum Template als eigene Datei, damit Service-
/// Registrierungen nicht im App-Lebenszyklus-Code aus Program.cs versteckt sind.
/// </summary>
internal static class ServiceConfigurator
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());

        // App-Identität
        services.AddSingleton<IAppProfile, SchreibkraftProfile>();

        // Persistenz / Plattform
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<IAutostartService, WindowsAutostartService>();

        // Status / Logs in-memory
        services.AddSingleton<ITrayStatusService, InMemoryTrayStatusService>();
        services.AddSingleton<IProcessingFailureLog, InMemoryProcessingFailureLog>();
        services.AddSingleton<IProcessingHistoryLog, InMemoryProcessingHistoryLog>();
        services.AddSingleton<FileLogger>();

        // Hotkey / Audio / Input
        services.AddSingleton<IHotkeyService, LowLevelKeyboardHotkeyService>();
        services.AddSingleton<IAudioDeviceService, NAudioDeviceService>();
        services.AddSingleton<IAudioRecorder, NAudioRecorder>();
        services.AddSingleton<IInputInjector, ClipboardInputInjector>();
        services.AddSingleton<IFeedbackSoundService, NAudioFeedbackSoundService>();
        services.AddSingleton<IClipboardSourceCapture, WindowsClipboardSourceCapture>();

        // STT-Provider — jeweils mit eigenem HttpClient via typed-client API.
        services.AddHttpClient<OpenAiCompatibleSttService>();
        services.AddHttpClient<DeepgramSttService>();
        services.AddHttpClient<AzureSpeechSttService>();
        services.AddHttpClient<AssemblyAiSttService>();
        services.AddHttpClient<ElevenLabsSttService>();

        // LLM-Provider — analog.
        services.AddHttpClient<OpenAiCompatibleLlmService>();
        services.AddHttpClient<AnthropicLlmService>();
        services.AddHttpClient<GoogleGeminiLlmService>();

        // Routing-Fassaden, die an die richtige konkrete Implementierung per Provider-Name dispatchen.
        services.AddSingleton<ISttService, RoutingSttService>();
        services.AddSingleton<ILlmService, RoutingLlmService>();

        services.AddSingleton<SpeechPipeline>();

        return services.BuildServiceProvider();
    }
}
