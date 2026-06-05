using System.Globalization;
using Schreibkraft.Core;

namespace Schreibkraft;

public sealed class SchreibkraftProfile : IAppProfile
{
    public string AppName => "Schreibkraft";
    public string DataFolderName => "Schreibkraft";
    public string AuthorName => "Ronny Schulz";
    public string CopyrightText => "Copyright 2026 Ronny Schulz";
    public string LicenseName => "MIT License";
    public string MutexName => "Global\\Schreibkraft.Singleton";
    public string AumId => "RonnySchulz.Schreibkraft";
    public string AutostartRegistryValueName => "Schreibkraft";

    public string SystemPrompt => L.S("prompt.system.base");

    public IReadOnlyList<AssistantModeDefinition> Modes =>
    [
        new(
            AssistantMode.Transform,
            L.S("assistant.transform.name"),
            L.S("assistant.transform.description"),
            "Ctrl+Shift+1",
            L.S("prompt.mode.transform.default")),
        new(
            AssistantMode.Generate,
            L.S("assistant.generate.name"),
            L.S("assistant.generate.description"),
            "Ctrl+Shift+2",
            L.S("prompt.mode.generate.default")),
        new(
            AssistantMode.AnswerClipboard,
            L.S("assistant.answer.name"),
            L.S("assistant.answer.description"),
            "Ctrl+Shift+3",
            L.S("prompt.mode.answer.default"),
            RequiresClipboardSource: true)
    ];

    public IReadOnlyList<PromptTemplate> PromptTemplates =>
    [
        // Transform — four refinement levels from minimal to noticeable
        new(
            L.S("template.correction_min.title"),
            L.S("template.correction_min.description"),
            L.S("template.correction_min.text"),
            [AssistantMode.Transform]),
        new(
            L.S("template.correction.title"),
            L.S("template.correction.description"),
            L.S("template.correction.text"),
            [AssistantMode.Transform]),
        new(
            L.S("template.smooth.title"),
            L.S("template.smooth.description"),
            L.S("template.smooth.text"),
            [AssistantMode.Transform]),
        new(
            L.S("template.clarify.title"),
            L.S("template.clarify.description"),
            L.S("template.clarify.text"),
            [AssistantMode.Transform]),

        // Mode defaults (one each)
        new(
            L.S("template.reply.title"),
            L.S("template.reply.description"),
            L.S("template.reply.text"),
            [AssistantMode.AnswerClipboard]),
        new(
            L.S("template.free.title"),
            L.S("template.free.description"),
            L.S("template.free.text"),
            [AssistantMode.Generate])
    ];

    public string IntensityStepName(AssistantMode mode, int intensity)
    {
        var step = Math.Clamp(intensity, Defaults.MinModeIntensity, Defaults.MaxModeIntensity);
        var key = mode switch
        {
            AssistantMode.Transform => $"intensity.transform.name.{step}",
            AssistantMode.Generate => $"intensity.generate.name.{step}",
            AssistantMode.AnswerClipboard => $"intensity.answer.name.{step}",
            _ => null
        };
        return key is null ? step.ToString(CultureInfo.InvariantCulture) : L.S(key);
    }

    public string IntensityStepInstruction(AssistantMode mode, int intensity)
    {
        var step = Math.Clamp(intensity, Defaults.MinModeIntensity, Defaults.MaxModeIntensity);
        var key = mode switch
        {
            AssistantMode.Transform => $"intensity.transform.{step}",
            AssistantMode.Generate => $"intensity.generate.{step}",
            AssistantMode.AnswerClipboard => $"intensity.answer.{step}",
            _ => null
        };
        return key is null ? string.Empty : L.S(key);
    }

    public string WritingStyleInstruction(WritingStyle style) => style switch
    {
        WritingStyle.Casual => L.S("style.casual"),
        WritingStyle.Neutral => L.S("style.neutral"),
        WritingStyle.Professional => L.S("style.professional"),
        WritingStyle.Academic => L.S("style.academic"),
        _ => string.Empty
    };

    public string EmojiExpressionInstruction(EmojiExpression level) => level switch
    {
        EmojiExpression.None => L.S("emoji.none"),
        EmojiExpression.Sparse => L.S("emoji.sparse"),
        EmojiExpression.Balanced => L.S("emoji.balanced"),
        EmojiExpression.Lively => L.S("emoji.lively"),
        EmojiExpression.Heavy => L.S("emoji.heavy"),
        _ => string.Empty
    };

    public List<AssistantInstance> CreateDefaultAssistants() =>
        Modes.Select(mode => new AssistantInstance
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = mode.Mode,
            Name = mode.Name,
            Hotkey = mode.DefaultHotkey,
            Prompt = mode.DefaultPrompt,
            Intensity = Defaults.DefaultModeIntensity,
            WritingStyle = WritingStyle.Neutral,
            ParagraphDensity = ParagraphDensity.Balanced,
            EmojiExpression = EmojiExpression.Balanced
        }).ToList();
}
