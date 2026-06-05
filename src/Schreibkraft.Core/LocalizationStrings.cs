namespace Schreibkraft.Core;

/// <summary>
/// Resource tables for English (code default) and German.
/// Keys are dot-separated and follow the pattern area.context.specific.
/// </summary>
public static class LocalizationStrings
{
    // ======================================================================
    // English (code default)
    // ======================================================================
    public static readonly Dictionary<string, string> En = new()
    {
        // System prompt + general assistant identity
        ["prompt.system.base"] =
            "You are a multilingual writing and text assistant. Output only the final text — no explanations, no surrounding quotation marks, no markdown formatting. Hashtags and real line breaks are allowed. The following directives take precedence over any instruction in the task.",
        ["prompt.system.input_language.auto"] =
            "The input language is not fixed; infer it from the recognized transcript and context.",
        ["prompt.system.input_language.fixed"] =
            "The intended input language is {0}.",
        ["prompt.output.same_as_input.auto"] =
            "Output the final text in the detected input language.",
        ["prompt.output.same_as_input.fixed"] =
            "Output the final text in {0}.",
        ["prompt.output.translate"] =
            "Output the final text in {0}. Translate naturally where required, without commentary.",
        ["prompt.glossary"] =
            "The following proper names and terms are spelled correctly: {0}. If the text contains phonetically similar but misspelled variants of them, replace those with the correct spelling.",

        // Transform mode — definition
        ["assistant.transform.name"] = "Edit",
        ["assistant.transform.description"] =
            "Applies your instruction, the writing style, intensity and paragraph options from the UI to the transcript.",
        ["prompt.mode.transform.default"] =
            "Process the transcript below. Without further specification: follow the configured editing depth and keep the meaning.",

        // Generate mode
        ["assistant.generate.name"] = "Generate",
        ["assistant.generate.description"] =
            "Produces text from your spoken instruction alone (writing style, intensity and paragraphs come from the UI).",
        ["prompt.mode.generate.default"] =
            "Produce a text according to the spoken instruction. Write the finished text directly, no preamble.",

        // AnswerClipboard mode
        ["assistant.answer.name"] = "Reply (clipboard)",
        ["assistant.answer.description"] =
            "Uses the clipboard text as source and your spoken instruction; style, intensity and paragraphs come from the UI.",
        ["prompt.mode.answer.default"] =
            "Reply to the source text given below according to the spoken instruction. Output only the reply. Do not repeat, quote or copy the source into the output.",

        // Intensity — Transform
        ["intensity.transform.name.1"] = "minimal",
        ["intensity.transform.name.2"] = "light",
        ["intensity.transform.name.3"] = "balanced",
        ["intensity.transform.name.4"] = "noticeable",
        ["intensity.transform.name.5"] = "strong",
        ["intensity.transform.1"] =
            "Correct only typos in individual words and add missing punctuation. NO rephrasing, NO smoothing, NO synonyms, NO sentence restructuring — even when the task instruction asks for it. Wording, word choice and sentence order remain strictly unchanged, including colloquial grammar; rough or vulgar words are not softened.",
        ["intensity.transform.2"] =
            "Correct spelling, grammar and punctuation. Word choice and sentence structure remain largely intact; rough or vulgar words are not softened.",
        ["intensity.transform.3"] =
            "Correct errors and lightly reword sentences. Smooth spoken phrasing, reduce filler words and unnecessary repetition. Meaning and tonality stay the same; rough or vulgar words are not softened.",
        ["intensity.transform.4"] =
            "Make the text clearer and more readable. Restructuring sentences and adjusting word choice is permitted; the message stays the same.",
        ["intensity.transform.5"] =
            "Rework the text freely — reword, rearrange, change the style. The meaning of all statements stays intact; do not add new content.",

        // Intensity — Generate
        ["intensity.generate.name.1"] = "strictly literal",
        ["intensity.generate.name.2"] = "close to brief",
        ["intensity.generate.name.3"] = "balanced",
        ["intensity.generate.name.4"] = "freer",
        ["intensity.generate.name.5"] = "very creative",
        ["intensity.generate.1"] = "Stick strictly to the instruction and add no content of your own.",
        ["intensity.generate.2"] = "Follow the instruction closely and add only where strictly necessary.",
        ["intensity.generate.3"] = "Implement the instruction sensibly and add fitting content without drifting.",
        ["intensity.generate.4"] = "Implement the instruction generously and add helpful detail.",
        ["intensity.generate.5"] = "Implement the instruction very freely and creatively, adding fitting ideas.",

        // Intensity — AnswerClipboard
        ["intensity.answer.name.1"] = "very brief",
        ["intensity.answer.name.2"] = "compact",
        ["intensity.answer.name.3"] = "balanced",
        ["intensity.answer.name.4"] = "detailed",
        ["intensity.answer.name.5"] = "very detailed",
        ["intensity.answer.1"] = "Reply very briefly and strictly along the instruction, in at most 1-2 sentences.",
        ["intensity.answer.2"] = "Reply compactly, close to the brief, in 2-4 short sentences. Do not add extra explanatory or closing paragraphs.",
        ["intensity.answer.3"] = "Reply in a balanced way with the necessary detail.",
        ["intensity.answer.4"] = "Reply in detail and add helpful information.",
        ["intensity.answer.5"] = "Reply very thoroughly and cover the topic broadly.",

        // Writing style
        ["style.casual"] = "Writing style: casual and colloquial, short sentences and personal tone are welcome.",
        ["style.neutral"] = "Writing style: neutral and factual, neither formal nor too casual.",
        ["style.professional"] = "Writing style: professional and polite, clear structure, no filler.",
        ["style.academic"] = "Writing style: academic and precise, complete sentences, terminology where fitting, neutral tone.",
        ["style.label.casual"] = "casual",
        ["style.label.neutral"] = "neutral",
        ["style.label.professional"] = "professional",
        ["style.label.academic"] = "academic",

        // Emoji expression
        ["emoji.none"] = "Emojis: none. Factual, text only.",
        ["emoji.sparse"] = "Emojis: very sparingly at most, only when they clearly support the content.",
        ["emoji.balanced"] = "Emojis: occasionally, when fitting; not overloaded.",
        ["emoji.lively"] =
            "Emojis: lively social-media tone, multiple per paragraph allowed. At the end, add 3–6 matching hashtags on a separate line, space-separated.",
        ["emoji.heavy"] =
            "Emojis: generous and frequent, strong social-media character, still readable. At the end, add 5–10 matching hashtags on a separate line, space-separated.",
        ["emoji.label.none"] = "none",
        ["emoji.label.sparse"] = "sparse",
        ["emoji.label.balanced"] = "balanced",
        ["emoji.label.lively"] = "lively",
        ["emoji.label.heavy"] = "heavy",

        // Paragraph density
        ["paragraph.compact"] = "Paragraphs: keep coherent, no extra blank lines unless inherently required.",
        ["paragraph.spacious"] = "Paragraphs: split airily into short paragraphs, separate sense units with a blank line.",
        ["paragraph.balanced"] = "Paragraphs: sensible paragraphs and blank lines for readability, separate thematic sections.",
        ["paragraph.label.compact"] = "compact (few paragraphs)",
        ["paragraph.label.balanced"] = "balanced",
        ["paragraph.label.spacious"] = "spacious (more paragraphs)",

        // Prompt templates — Transform
        ["template.correction_min.title"] = "Typos only",
        ["template.correction_min.description"] = "Only typos in single words and missing punctuation. Wording stays.",
        ["template.correction_min.text"] =
            "Correct only typos in individual words and add missing punctuation. Do not change wording, word choice or sentence order. Return the corrected text only.",
        ["template.correction.title"] = "Correction",
        ["template.correction.description"] = "Spelling, grammar and punctuation. No rephrasing.",
        ["template.correction.text"] =
            "Correct only spelling, grammar and punctuation. Do not change wording or rephrase the content. Return the corrected text only.",
        ["template.smooth.title"] = "Smooth",
        ["template.smooth.description"] = "Light cleanup: remove fillers, fix grammar; meaning stays.",
        ["template.smooth.text"] =
            "Fix spelling and grammar and remove spoken filler words (e.g. uh, well). Keep meaning, tone and roughly the same wording. Return only the cleaned-up text.",
        ["template.clarify.title"] = "Clearer wording",
        ["template.clarify.description"] = "Make the text clearer and more readable. Meaning stays.",
        ["template.clarify.text"] =
            "Rewrite the text so it reads clearer and more naturally. Restructuring sentences and adjusting word choice is allowed; the meaning stays the same. Return only the final text.",

        // Mode defaults
        ["template.reply.title"] = "Reply to source",
        ["template.reply.description"] = "Reply to clipboard text according to the spoken instruction.",
        ["template.reply.text"] =
            "Reply to the source text below according to the spoken instruction. Output only the reply; do not repeat, quote or copy the source.",
        ["template.free.title"] = "Free generation",
        ["template.free.description"] = "Generates text from the spoken instruction alone.",
        ["template.free.text"] =
            "Produce a text according to the spoken instruction. Write the finished text directly, no preamble.",

        // Navigation
        ["nav.overview"] = "Overview",
        ["nav.audio_language"] = "Audio & language",
        ["nav.processing"] = "Processing",
        ["nav.assistants"] = "Assistants",
        ["nav.spelling"] = "Spelling correction",
        ["nav.general"] = "General",
        ["nav.diagnostics"] = "Diagnostics",
        ["nav.about"] = "About",

        // Page titles fallback
        ["page.title.overview"] = "Overview",
        ["page.title.audio_language"] = "Audio & language",
        ["page.title.processing"] = "Processing",
        ["page.title.assistants"] = "Assistants",
        ["page.title.spelling"] = "Spelling correction",
        ["page.title.general"] = "General",
        ["page.title.diagnostics"] = "Diagnostics",
        ["page.title.about"] = "About",

        // Common buttons / actions
        ["common.add"] = "Add",
        ["common.remove"] = "Remove",
        ["common.delete"] = "Delete",
        ["common.cancel"] = "Cancel",
        ["common.confirm"] = "Confirm",
        ["common.replace"] = "Replace",
        ["common.refresh"] = "Refresh",
        ["common.copy"] = "Copy",
        ["common.open_settings"] = "Open settings",
        ["common.save"] = "Save",
        ["common.close"] = "Close",
        ["common.restart_required"] = "Restart required for changes to take effect.",

        // Spelling page
        ["spelling.title"] = "Spelling correction",
        ["spelling.intro"] =
            "Reusable sets of word replacements. Each assistant selects which sets apply to the AI response. Replacements are case-sensitive and only match whole words.",
        ["spelling.add_set"] = "+ add new set",
        ["spelling.delete_set"] = "Delete set",
        ["spelling.no_sets"] = "No sets yet.",
        ["spelling.row.from"] = "from",
        ["spelling.row.to"] = "to",
        ["spelling.row.delete_tooltip"] = "Remove entry",
        ["spelling.add_row"] = "+ add entry",
        ["spelling.terms.title"] = "Proper names / terms",
        ["spelling.terms.intro"] = "Comma-separated. Given to the AI as correct spelling — it fixes phonetically similar mistranscriptions.",
        ["spelling.terms.placeholder"] = "Mahlsdorf, RDSport, Renate",
        ["spelling.set_name"] = "Name",
        ["spelling.new_set_default_name"] = "new set",
        ["spelling.assistant_section.title"] = "Spelling correction",
        ["spelling.assistant_section.empty"] = "No sets defined yet. Create one on the “Spelling correction” page.",
        ["spelling.assistant_section.intro"] = "Choose which sets apply to this assistant's AI response.",
        ["spelling.assistant_section.unnamed"] = "(unnamed)",
        ["spelling.assistant_section.manage"] = "manage sets…",

        // Assistant card / sliders
        ["assistant.intensity.transform"] = "Editing depth (transcript)",
        ["assistant.intensity.generate"] = "Generation freedom",
        ["assistant.intensity.answer"] = "Reply level of detail",
        ["assistant.intensity.generic"] = "Intensity",
        ["assistant.writing_style.header"] = "Writing style",
        ["assistant.paragraphs.header"] = "Paragraphs and blank lines",
        ["assistant.emojis.header"] = "Emojis and social tone",
        ["assistant.instruction.placeholder"] = "Instruction",

        // Tray menu
        ["tray.open"] = "open",
        ["tray.active"] = "active",
        ["tray.exit"] = "exit",

        // Language picker
        ["language.label"] = "Language",
        ["language.auto"] = "System default",
        ["language.english"] = "English",
        ["language.german"] = "German",
        ["language.restart_hint"] = "Language changes apply on the next start of the app.",

        // Common labels
        ["common.cancel"] = "Cancel",
        ["common.ok"] = "OK",
        ["common.update"] = "Refresh",
        ["common.reset"] = "Reset",
        ["common.note"] = "Note",
        ["common.version"] = "Version",
        ["common.author"] = "Author",
        ["common.architecture"] = "Architecture",
        ["common.tech"] = "Tech stack",
        ["common.assistant"] = "Assistant",
        ["common.time"] = "Time",
        ["common.insert_method"] = "Insert method",
        ["common.via_clipboard"] = "via clipboard",
        ["common.directly_typed"] = "directly typed",

        // Status / tray
        ["status.ready"] = "Ready",
        ["status.ready.hint"] = "Ready. Hold an assistant hotkey to dictate.",
        ["status.ready_to_dictate"] = "Ready to dictate",
        ["status.recording"] = "Recording",
        ["status.processing"] = "Processing",
        ["status.recording_in_progress"] = "Recording in progress",
        ["status.success"] = "Success",
        ["status.error"] = "Error",
        ["status.inactive"] = "Inactive",
        ["status.setup_required"] = "Setup required",
        ["status.setup_required.long"] = "Setup required. Please review the highlighted settings.",
        ["status.please_check"] = "Please check provider, API key and assistant hotkeys.",
        ["status.api_key_needed"] = "Please enter or save an API key for AI processing.",

        // Overview page
        ["overview.intro"] =
            "Assistant for push-to-talk spoken input: transcription, AI text processing, and inserting into active applications.",
        ["overview.feature_scope"] = "Features",
        ["overview.feature_scope.body"] =
            "The app records speech only while a hotkey is held, transcribes it via the configured cloud provider, and processes the text according to your instruction, writing style, intensity and paragraph options in the assistant card.",
        ["overview.feature_scope.translation"] =
            "Input and output languages can be set independently, so processing can also act as translation.",
        ["overview.feature_scope.type"] =
            "The assistant type only selects the source (transcript, instruction-only, or clipboard); fine control happens per-hotkey in the additional fields.",
        ["overview.test_field"] = "Test field",
        ["overview.test_field.body"] =
            "Try out insertion here: click into the field, hold an assistant hotkey and speak. The final text will appear in this field.",
        ["overview.test_field.placeholder"] = "Insertion test writes into this field only.",

        // Audio & Language page
        ["audio.input"] = "Audio input",
        ["audio.input.intro"] = "Choose the microphone used while holding the hotkey.",
        ["audio.source"] = "Audio source",
        ["audio.refresh_devices"] = "Refresh devices",
        ["audio.devices_updated"] = "Audio devices were refreshed. Save the settings to apply the selection.",
        ["language.section.title"] = "Language",
        ["language.section.intro"] =
            "The assistant can translate by recognizing the input language and producing output in a chosen one.",
        ["language.input"] = "Input language",
        ["language.output"] = "Output language",
        ["language.input.assistant"] = "Input language (assistant)",
        ["language.output.assistant"] = "Output language (assistant)",

        // Pipeline page
        ["pipeline.transcription"] = "Transcription",
        ["pipeline.ai_processing"] = "AI processing",
        ["pipeline.insertion"] = "Insertion",
        ["pipeline.provider"] = "Provider",
        ["pipeline.model"] = "Model",
        ["pipeline.api_key.stt"] = "API key for transcription",
        ["pipeline.api_key.llm"] = "API key for AI processing",
        ["pipeline.api_key.placeholder"] = "OpenAI API key (sk-…)",
        ["pipeline.api_key.saved_placeholder"] = "API key is saved. Enter a new key to replace it.",
        ["pipeline.api_key.new_placeholder"] = "Enter new API key",
        ["pipeline.api_key.saved_status"] = "API key saved.",
        ["pipeline.api_key.missing_status"] = "No API key saved.",
        ["pipeline.api_key.replace"] = "Replace key",
        ["pipeline.api_key.note"] =
            "API keys are stored locally for the current Windows user encrypted with DPAPI.",
        ["pipeline.test_connection"] = "Test connection",
        ["pipeline.connection_test"] = "Connection test",
        ["pipeline.connection_test.prompt"] = "Reply exactly with OK.",
        ["pipeline.connection_test.success"] = "AI connection tested successfully.",
        ["pipeline.connection_test.failure"] = "AI connection failed. Please check API key and model.",
        ["pipeline.model.suggest_stt"] = "Choose from the list or enter your own model ID (e.g. gpt-4o-mini-transcribe)",
        ["pipeline.model.suggest_llm"] = "Choose from the list or enter your own model ID (e.g. gpt-5.1)",
        ["pipeline.retries"] = "Retries on failure (0–5)",
        ["pipeline.insert_method"] = "Insert method",
        ["pipeline.restore_clipboard"] = "Restore clipboard after insertion",
        ["pipeline.custom_endpoint"] = "Custom endpoint URL",
        ["pipeline.custom_endpoint.placeholder_stt"] = "e.g. https://api.groq.com/openai/v1/audio/transcriptions",
        ["pipeline.custom_endpoint.placeholder_llm"] = "e.g. https://api.groq.com/openai/v1/chat/completions",
        ["validation.custom_endpoint_missing"] = "Please enter the custom endpoint URL.",
        ["general.launch_minimized"] = "Start minimized to the system tray",
        ["general.minimize_to_tray"] = "Hide to the system tray on minimize",
        ["general.autostart"] = "Start with Windows",
        ["assistant.system_prompt_override"] = "Custom system prompt",
        ["common.unknown"] = "unknown",

        // Assistants page
        ["assistants.intro"] =
            "You can add any number of assistants. The type only determines whether a transcript, an instruction alone, or the clipboard is the source; style, intensity and paragraphs are controlled per card.",
        ["assistants.add_new"] = "Add new assistant",
        ["assistants.add_button"] = "+ Add assistant",
        ["assistants.new_type"] = "Type of the new assistant",
        ["assistants.empty"] = "No assistants yet. Create one above.",
        ["assistants.delete"] = "Delete assistant",
        ["assistants.delete.confirm.title"] = "Delete assistant?",
        ["assistants.drop_caption"] = "Move here",
        ["assistants.drag_handle"] = "Drag to reorder",
        ["assistants.delete.confirm.body"] = "Remove “{0}” permanently from the list?",
        ["assistants.display_name"] = "Display name",
        ["assistants.assistant_type"] = "Assistant type",
        ["assistants.hotkey.capture"] = "Capture hotkey",
        ["assistants.hotkey.prompt"] = "Press hotkey combination …",
        ["assistants.hotkey.aborted"] = "Hotkey capture cancelled.",
        ["assistants.hotkey.press_now"] = "Press the desired key combination. Escape cancels.",
        ["assistants.hotkey.applied"] = "Hotkey saved and active.",
        ["assistants.hotkey.applied_cleared"] = "Hotkey saved. For {0} the same combination was cleared — each key can only belong to one assistant.",
        ["assistants.hotkey.register_failed"] = "This hotkey could not be registered. Please pick a different combination.",
        ["assistants.template"] = "Template",
        ["assistants.template.apply"] = "Apply a predefined instruction into the field",
        ["assistants.template.none"] = "No matching templates",
        ["assistants.template.replace.title"] = "Replace instruction?",
        ["assistants.template.replace.body"] =
            "The current text in the instruction field will be replaced by the template. Continue?",
        ["assistants.template.replace.button"] = "Replace",

        // General page
        ["general.startup"] = "Startup behavior and limits",
        ["general.max_recording_seconds"] = "Maximum recording duration",
        ["general.timeout_seconds"] = "Processing timeout (seconds)",
        ["general.play_sounds"] = "Play notification sounds at recording start and end",
        ["general.sound.volume"] = "Notification sound volume",
        ["general.sound.volume.value"] = "{0}%",
        ["general.sound.test"] = "Test",
        ["general.reset"] = "Restore defaults",
        ["general.reset.intro"] =
            "Resets all settings to default values. API keys remain unchanged.",
        ["general.reset.done"] = "Defaults applied and saved.",

        // Diagnostics page
        ["diagnostics.title"] = "Diagnostics",
        ["diagnostics.history.title"] = "Processing history",
        ["diagnostics.history.intro"] =
            "When enabled, the last 5 successful processings are stored (time, assistant, transcript, AI response). Helpful to clarify e.g. whether paragraphs come from the AI or are lost on insertion. Disabling clears the buffer immediately.",
        ["diagnostics.history.clear_now"] = "Clear history now",
        ["diagnostics.history.cleared"] = "Processing history cleared.",
        ["diagnostics.copy"] = "Copy (may include history)",
        ["diagnostics.copy.success"] = "Diagnostics copied to clipboard.",
        ["diagnostics.copy.failure"] = "Could not copy diagnostics.",
        ["diagnostics.open_log"] = "Open log file",
        ["diagnostics.open_log.failure"] = "Could not open the log file.",
        ["diagnostics.open"] = "Open diagnostics",
        ["diagnostics.history.latest_header"] = "Recent processings (newest first, max. ",
        ["diagnostics.history.keep_label"] =
            "Keep processing history for diagnostics (max 5 entries, contains transcripts and AI responses)",

        // About page
        ["about.copyright"] = "Copyright",
        ["about.license"] = "License",
        ["about.open_license"] = "Open license",
        ["about.license.body"] =
            "Schreibkraft is published as open-source software. You may use, copy, modify, redistribute and create your own variants of the app.",
        ["about.license.body2"] =
            "Distributed under the MIT License. The copyright notice by Ronny Schulz and the license text must be retained when redistributing.",
        ["about.license.body3"] = "The software is provided without warranty.",
        ["about.license.not_found"] = "The license file was not found in the install directory.",
        ["about.license.open_failed"] = "Could not open the license file.",
        ["about.third_party.open"] = "Open third-party notices",
        ["about.third_party.not_found"] = "Third-party notices were not found in the install directory.",
        ["about.third_party.open_failed"] = "Could not open third-party notices.",
        ["about.third_party.header"] = "Third-party components",
        ["about.third_party.description"] = "List of all bundled NuGet packages.",
        ["about.local_data.header"] = "Local data",
        ["about.local_data.description"] = "Paths to settings and logs.",
        ["about.privacy.title"] = "Privacy and processing",
        ["about.privacy.body"] =
            "For transcription and AI processing, audio or text is sent to the configured provider. Which data is processed there depends on its terms.",
        ["about.privacy.body2"] =
            "Audio data, transcripts and final texts are not stored in logs by default. Logs contain technical status information and error details.",
        ["about.tech.intro"] = "WinUI 3 / Windows App SDK",
        ["about.tech.dependency"] = "Service-oriented with dependency injection",
        ["about.tech.open_source"] = "Open source",
        ["about.tech.runtime"] = "Runtime",

        // Misc errors
        ["error.settings_save"] = "Could not save settings: {0}",
        ["error.settings_save.access"] = "Settings could not be saved because Windows denied access. Path: {0}",
        ["error.settings_save.io"] = "Settings could not be saved because the file is currently unavailable. Path: {0}",
        ["error.settings_save.path"] = "Settings could not be saved because the settings folder is unavailable. Path: {0}",
        ["error.settings_save.generic"] = "Settings could not be saved. Path: {0}",
        ["error.page.open"] = "Could not open page",
        ["error.page.open.body"] = "The page \"{0}\" could not be opened.",
        ["error.page.open_with_tag"] = "Could not open \"{0}\".",
        ["error.config"] = "Configuration",

        // Diagnostics labels
        ["diag.label.app_version"] = "App version",
        ["diag.label.assembly_version"] = "App version (assembly)",
        ["diag.label.exe_product_version"] = "App version (exe/product)",
        ["diag.label.exe"] = "Exe",
        ["diag.label.net_runtime"] = ".NET runtime",
        ["diag.label.data_dir"] = "App data path",
        ["diag.label.log_dir"] = "Logs",
        ["diag.label.stt_provider"] = "Transcription provider",
        ["diag.label.stt_model"] = "Transcription model",
        ["diag.label.audio_source"] = "Audio source",
        ["diag.label.input_lang"] = "Input language",
        ["diag.label.output_lang"] = "Output language",
        ["diag.label.llm_provider"] = "AI provider",
        ["diag.label.llm_model"] = "AI model",
        ["diag.label.hotkey_status"] = "Hotkey status",
        ["diag.label.hotkey_status.check"] = "check",
        ["diag.label.hotkey_status.valid"] = "valid",
        ["diag.label.setup_status"] = "Setup status",
        ["diag.label.status"] = "Status",
        ["diag.unknown"] = "unknown",
        ["diag.elevated_note"] =
            "Note: Inserting into elevated target apps can fail unless Schreibkraft also runs elevated.",
        ["diag.history.empty"] = "Recent processings: (none since activation)",
        ["diag.history.header"] = "Recent processings (newest first, max. {0}):",
        ["diag.entry.time"] = "Time",
        ["diag.entry.assistant"] = "Assistant",
        ["diag.entry.insert_method"] = "Insert method",
        ["diag.entry.timings"] = "Durations",
        ["diag.entry.timings.value"] = "total {0} | recording {1} | transcription {2} | AI {3} | insertion {4}",
        ["diag.entry.timings.recorder_stop"] = "recorder stop",
        ["diag.entry.system_prompt"] = "System prompt (to AI)",
        ["diag.entry.mode_prompt"] = "Mode prompt (to AI, with instruction/source)",
        ["diag.entry.transcript"] = "Transcript (appended to AI as user message)",
        ["diag.entry.ai_response"] = "AI response (raw, before insertion)",

        ["settings.saved.status"] = "Settings saved. Status={0}",
        ["error.no_recording_device"] = "No working recording device available.",
        ["status.recording_release"] = "Recording … release to process.",
        ["status.inactive.long"] = "Inactive. Activate the app from the tray menu to use hotkeys.",
        ["error.mic_access"] = "Microphone access failed. Please check the microphone and Windows privacy settings.",
        ["error.tray_window"] = "Could not create tray window.",
        ["validation.stt_model"] = "Please choose a transcription model.",
        ["validation.llm_model"] = "Please choose an AI model.",
        ["validation.stt_api_key"] = "Please save an API key for transcription.",
        ["validation.llm_api_key"] = "Please save an API key for AI processing.",
        ["validation.stt_provider"] = "Please choose a transcription provider.",
        ["validation.llm_provider"] = "Please choose an AI provider.",
        ["validation.stt_unsupported"] = "The selected transcription provider is not supported yet.",
        ["validation.llm_unsupported"] = "The selected AI provider is not supported yet.",
        ["validation.no_assistants"] = "No assistant configured. Please add at least one.",
        ["hotkey.register_failed"] = "Global hotkeys could not be registered.",
        ["audio.recording_failed"] = "Audio recording ended with error.",
        ["clipboard.locked"] = "Clipboard could not be opened.",
        ["clipboard.alloc_failed"] = "Clipboard memory could not be allocated.",
        ["clipboard.lock_failed"] = "Clipboard memory could not be locked.",
        ["clipboard.set_failed"] = "Clipboard could not be set.",
        ["clipboard.not_expected"] = "Clipboard does not contain the expected text.",
        ["error.input_send"] = "Keyboard input could not be sent ({0}/{1} events, Win32 error {2}). ",
        ["error.input_send.causes"] = "Possible causes: the target application runs with higher privileges (UIPI blocks input), ",
        ["error.input_send.middle"] = "the input was blocked by the OS, or the target window does not accept SendInput. ",
        ["error.input_send.tip"] = "Tip: in the settings under “Insertion”, switch the method to “via clipboard”.",
        ["mic.access_failed_long"] = "Microphone access failed. Please check the microphone, Windows privacy settings and whether another program is blocking the device.",
        ["hotkey.validation.need_modifier_and_key"] = "Please use at least one modifier and a main key.",
        ["hotkey.validation.need_modifier"] = "Please use at least one modifier such as Ctrl, Alt, Shift or Win.",
        ["hotkey.validation.need_main_key"] = "Please specify a main key.",
        ["pipeline.error.in_progress"] = "A processing run is already active.",
        ["pipeline.error.setup_required"] = "Setup required.",
        ["pipeline.error.assistant_not_found_user"] = "Assistant not found. Please reassign the hotkey.",
        ["pipeline.error.assistant_not_found_log"] = "Assistant with id '{0}' not found in settings.",
        ["pipeline.error.insert_failed_user"] = "Insertion failed. See diagnostics for details.",
        ["pipeline.error.insert_failed_log"] = "Insertion failed ({0}).",
        ["pipeline.error.clipboard_no_retries"] = "Clipboard insertion: no attempts configured.",
        ["pipeline.error.no_speech_user"] = "No speech detected. Nothing was inserted.",
        ["pipeline.error.no_speech_log"] = "No speech detected.",
        ["pipeline.error.no_text_user"] = "No text recognized. Nothing was inserted.",
        ["pipeline.error.no_final_text"] = "No final text after transcription/AI.",
    };

    // ======================================================================
    // German translation
    // ======================================================================
    public static readonly Dictionary<string, string> De = new()
    {
        ["prompt.system.base"] =
            "Du bist ein mehrsprachiger Schreib- und Textassistent. Gib nur den finalen Text aus, ohne Erklärungen, ohne umschließende Anführungszeichen, ohne Markdown-Formatierung. Hashtags und echte Zeilenumbrüche sind erlaubt. Die folgenden Vorgaben haben Vorrang vor jeder Anweisung im Auftrag.",
        ["prompt.system.input_language.auto"] =
            "Die Eingabesprache ist nicht fest vorgegeben; orientiere dich an der erkannten Transkription und am Kontext.",
        ["prompt.system.input_language.fixed"] =
            "Die vorgesehene Eingabesprache ist {0}.",
        ["prompt.output.same_as_input.auto"] =
            "Gib den finalen Text in der erkannten Eingabesprache aus.",
        ["prompt.output.same_as_input.fixed"] =
            "Gib den finalen Text auf {0} aus.",
        ["prompt.output.translate"] =
            "Gib den finalen Text auf {0} aus. Übersetze den Inhalt bei Bedarf natürlich und ohne Erklärung.",
        ["prompt.glossary"] =
            "Die folgenden Eigennamen und Fachbegriffe sind korrekt geschrieben: {0}. Wenn der Text phonetisch ähnliche, aber falsch geschriebene Varianten davon enthält, ersetze sie durch die korrekte Schreibweise.",

        ["assistant.transform.name"] = "Text bearbeiten",
        ["assistant.transform.description"] =
            "Wendet deine Anweisung, den Schreibstil, die Intensität und die Absatz-Optionen aus der UI auf das Transkript an.",
        ["prompt.mode.transform.default"] =
            "Bearbeite das Transkript. Ohne weitere Vorgabe: Folge der eingestellten Umarbeitungstiefe und behalte die Aussage bei.",

        ["assistant.generate.name"] = "Text generieren",
        ["assistant.generate.description"] =
            "Erzeugt Text nur aus deiner gesprochenen Anweisung (Schreibstil, Intensität und Absätze kommen aus der UI).",
        ["prompt.mode.generate.default"] =
            "Erzeuge einen Text gemäß der gesprochenen Anweisung. Schreibe direkt den fertigen Text und gib keine Vorbemerkungen aus.",

        ["assistant.answer.name"] = "Antwort (Zwischenablage)",
        ["assistant.answer.description"] =
            "Nutzt den Text in der Zwischenablage als Quelle und deine gesprochene Anweisung; Stil, Intensität und Absätze steuerst du in der UI.",
        ["prompt.mode.answer.default"] =
            "Beantworte den unten angegebenen Quelltext gemäß der gesprochenen Anweisung. Gib ausschließlich die Antwort aus. Wiederhole, zitiere oder kopiere den Quelltext nicht in die Ausgabe.",

        ["intensity.transform.name.1"] = "minimal",
        ["intensity.transform.name.2"] = "leicht",
        ["intensity.transform.name.3"] = "ausgewogen",
        ["intensity.transform.name.4"] = "deutlich",
        ["intensity.transform.name.5"] = "stark",
        ["intensity.transform.1"] =
            "Nur Tippfehler in einzelnen Wörtern korrigieren und fehlende Satzzeichen ergänzen. KEINE Umformulierung, KEIN Glätten, KEINE Synonyme, KEINE Satzumstellung — auch dann nicht, wenn die Auftrags-Anweisung das verlangt. Wortlaut, Wortwahl und Satzstellung bleiben strikt unverändert, einschließlich umgangssprachlicher Grammatik; derbe oder vulgäre Wörter werden nicht abgeschwächt.",
        ["intensity.transform.2"] =
            "Rechtschreibung, Grammatik und Zeichensetzung korrigieren. Wortwahl und Satzstellung bleiben weitgehend erhalten; derbe oder vulgäre Wörter werden nicht abgeschwächt.",
        ["intensity.transform.3"] =
            "Fehler korrigieren und Sätze leicht umformulieren. Gesprochene Formulierungen glätten, Füllwörter und unnötige Wiederholungen reduzieren. Aussage und Tonalität bleiben gleich; derbe oder vulgäre Wörter werden nicht abgeschwächt.",
        ["intensity.transform.4"] =
            "Text klarer und lesbarer formulieren. Sätze umbauen und Wortwahl anpassen sind erlaubt; die Aussage bleibt gleich.",
        ["intensity.transform.5"] =
            "Text frei überarbeiten — umformulieren, umstellen, im Stil verändern. Der Sinn aller Aussagen bleibt erhalten, keine neuen Inhalte hinzufügen.",

        ["intensity.generate.name.1"] = "streng wörtlich",
        ["intensity.generate.name.2"] = "nah am Auftrag",
        ["intensity.generate.name.3"] = "ausgewogen",
        ["intensity.generate.name.4"] = "freier",
        ["intensity.generate.name.5"] = "sehr kreativ",
        ["intensity.generate.1"] = "Halte dich strikt an die Anweisung und ergänze keine eigenen Inhalte.",
        ["intensity.generate.2"] = "Folge der Anweisung eng und ergänze nur, wo unbedingt nötig.",
        ["intensity.generate.3"] = "Setze die Anweisung sinnvoll um und ergänze passend, ohne abzuschweifen.",
        ["intensity.generate.4"] = "Setze die Anweisung großzügig um und ergänze hilfreiche Details.",
        ["intensity.generate.5"] = "Setze die Anweisung sehr frei und kreativ um, ergänze passende Ideen.",

        ["intensity.answer.name.1"] = "sehr knapp",
        ["intensity.answer.name.2"] = "kompakt",
        ["intensity.answer.name.3"] = "ausgewogen",
        ["intensity.answer.name.4"] = "ausführlich",
        ["intensity.answer.name.5"] = "sehr ausführlich",
        ["intensity.answer.1"] = "Antworte sehr knapp und strikt entlang der Anweisung, mit höchstens 1-2 Sätzen.",
        ["intensity.answer.2"] = "Antworte kompakt und nah am Auftrag, mit 2-4 kurzen Sätzen. Ergänze keine zusätzlichen Erklär- oder Schlussabsätze.",
        ["intensity.answer.3"] = "Antworte ausgewogen mit den nötigen Details.",
        ["intensity.answer.4"] = "Antworte ausführlich und ergänze hilfreiche Details.",
        ["intensity.answer.5"] = "Antworte sehr ausführlich und decke das Thema breit ab.",

        ["style.casual"] = "Schreibstil: locker und alltagssprachlich, gerne kurze Sätze und persönliche Ansprache.",
        ["style.neutral"] = "Schreibstil: neutral und sachlich, weder formell noch zu locker.",
        ["style.professional"] = "Schreibstil: professionell und höflich, klare Struktur, ohne Floskeln.",
        ["style.academic"] = "Schreibstil: wissenschaftlich-präzise, vollständige Sätze, Fachbegriffe wo passend, neutraler Ton.",
        ["style.label.casual"] = "locker",
        ["style.label.neutral"] = "neutral",
        ["style.label.professional"] = "professionell",
        ["style.label.academic"] = "wissenschaftlich",

        ["emoji.none"] = "Emojis: keine. Sachlich, rein textbasiert.",
        ["emoji.sparse"] = "Emojis: höchstens sehr sparsam, nur wenn sie den Inhalt klar unterstützen.",
        ["emoji.balanced"] = "Emojis: gelegentlich, wenn sie zum Kontext passen; nicht überladen.",
        ["emoji.lively"] =
            "Emojis: lebhafter Social-Media-Ton, mehrere pro Abschnitt erlaubt. Am Ende 3–6 passende Hashtags in einer eigenen Zeile, leerzeichengetrennt.",
        ["emoji.heavy"] =
            "Emojis: großzügig und häufig, starker Social-Media-Charakter, bleibt lesbar. Am Ende 5–10 passende Hashtags in einer eigenen Zeile, leerzeichengetrennt.",
        ["emoji.label.none"] = "keine",
        ["emoji.label.sparse"] = "sparsam",
        ["emoji.label.balanced"] = "ausgewogen",
        ["emoji.label.lively"] = "lebhaft",
        ["emoji.label.heavy"] = "stark",

        ["paragraph.compact"] = "Absätze: zusammenhängend halten, keine zusätzlichen Leerzeilen außer inhaltlich zwingend.",
        ["paragraph.spacious"] = "Absätze: luftig in kurze Absätze gliedern, Sinnabschnitte durch Leerzeile getrennt.",
        ["paragraph.balanced"] = "Absätze: sinnvolle Absätze und Leerzeilen für bessere Lesbarkeit, thematische Abschnitte trennen.",
        ["paragraph.label.compact"] = "kompakt (wenig Absätze)",
        ["paragraph.label.balanced"] = "ausgewogen",
        ["paragraph.label.spacious"] = "luftig (mehr Absätze)",

        // Prompt-Vorlagen — Transform
        ["template.correction_min.title"] = "nur Tippfehler",
        ["template.correction_min.description"] = "Nur Tippfehler in einzelnen Wörtern und fehlende Satzzeichen. Wortlaut bleibt.",
        ["template.correction_min.text"] =
            "Korrigiere nur Tippfehler in einzelnen Wörtern und ergänze fehlende Satzzeichen. Wortlaut, Wortwahl und Satzstellung bleiben unverändert. Gib nur den korrigierten Text zurück.",
        ["template.correction.title"] = "Korrektur",
        ["template.correction.description"] = "Rechtschreibung, Grammatik und Zeichensetzung. Keine Umformulierung.",
        ["template.correction.text"] =
            "Bearbeite das Transkript gemäß dieser Anweisung. Ohne weitere Vorgabe: Rechtschreibung und Grammatik korrigieren, Aussage beibehalten. Keine Änderung der Wortwahl.",
        ["template.smooth.title"] = "leicht glätten",
        ["template.smooth.description"] = "Fehler korrigieren, Füllwörter entfernen; Aussage bleibt.",
        ["template.smooth.text"] =
            "Korrigiere Rechtschreibung und Grammatik und entferne Sprech-Füllwörter (z. B. „äh“, „also“). Aussage, Tonalität und Wortwahl bleiben weitgehend erhalten. Gib nur den bearbeiteten Text zurück.",
        ["template.clarify.title"] = "klarer formulieren",
        ["template.clarify.description"] = "Text klarer und lesbarer machen. Aussage bleibt.",
        ["template.clarify.text"] =
            "Formuliere den Text klarer und lesbarer. Sätze umbauen und Wortwahl anpassen ist erlaubt; die Aussage bleibt gleich. Gib nur den finalen Text zurück.",

        // Mode-Defaults
        ["template.reply.title"] = "Antwort auf Quelltext",
        ["template.reply.description"] = "Beantwortet den Zwischenablage-Text gemäß gesprochener Anweisung.",
        ["template.reply.text"] =
            "Beantworte den unten angegebenen Quelltext gemäß der gesprochenen Anweisung. Gib ausschließlich die Antwort aus. Wiederhole, zitiere oder kopiere den Quelltext nicht in die Ausgabe.",
        ["template.free.title"] = "frei generieren",
        ["template.free.description"] = "Erzeugt einen Text nur aus der gesprochenen Anweisung.",
        ["template.free.text"] =
            "Erzeuge einen Text gemäß der gesprochenen Anweisung. Schreibe direkt den fertigen Text und gib keine Vorbemerkungen aus.",

        ["nav.overview"] = "Übersicht",
        ["nav.audio_language"] = "Audio & Sprache",
        ["nav.processing"] = "Verarbeitung",
        ["nav.assistants"] = "Assistenten",
        ["nav.spelling"] = "Rechtschreibkorrektur",
        ["nav.general"] = "Allgemein",
        ["nav.diagnostics"] = "Diagnose",
        ["nav.about"] = "Über",

        ["page.title.overview"] = "Übersicht",
        ["page.title.audio_language"] = "Audio & Sprache",
        ["page.title.processing"] = "Verarbeitung",
        ["page.title.assistants"] = "Assistenten",
        ["page.title.spelling"] = "Rechtschreibkorrektur",
        ["page.title.general"] = "Allgemein",
        ["page.title.diagnostics"] = "Diagnose",
        ["page.title.about"] = "Über",

        ["common.add"] = "hinzufügen",
        ["common.remove"] = "entfernen",
        ["common.delete"] = "löschen",
        ["common.cancel"] = "abbrechen",
        ["common.confirm"] = "bestätigen",
        ["common.replace"] = "ersetzen",
        ["common.refresh"] = "aktualisieren",
        ["common.copy"] = "kopieren",
        ["common.open_settings"] = "Einstellungen öffnen",
        ["common.save"] = "speichern",
        ["common.close"] = "schließen",
        ["common.restart_required"] = "Neustart erforderlich, damit die Änderungen wirken.",

        ["spelling.title"] = "Rechtschreibkorrektur",
        ["spelling.intro"] =
            "Wiederverwendbare Sets von Wort-Ersetzungen. Pro Assistent wird ausgewählt, welche Sets auf die KI-Antwort angewendet werden. Ersetzungen sind case-sensitiv und greifen nur auf ganze Wörter.",
        ["spelling.add_set"] = "+ neues Set anlegen",
        ["spelling.delete_set"] = "Set löschen",
        ["spelling.no_sets"] = "Noch keine Sets vorhanden.",
        ["spelling.row.from"] = "von",
        ["spelling.row.to"] = "zu",
        ["spelling.row.delete_tooltip"] = "Eintrag entfernen",
        ["spelling.add_row"] = "+ Eintrag hinzufügen",
        ["spelling.terms.title"] = "Eigennamen / Fachbegriffe",
        ["spelling.terms.intro"] = "Kommagetrennt. Wird der KI als korrekte Schreibweise mitgegeben — sie korrigiert phonetisch ähnliche Fehltranskripte.",
        ["spelling.terms.placeholder"] = "Mahlsdorf, RDSport, Renate",
        ["spelling.set_name"] = "Name",
        ["spelling.new_set_default_name"] = "neues Set",
        ["spelling.assistant_section.title"] = "Rechtschreibkorrektur",
        ["spelling.assistant_section.empty"] = "Noch keine Sets angelegt. Auf der Seite „Rechtschreibkorrektur“ erstellen.",
        ["spelling.assistant_section.intro"] = "Wähle, welche Sets auf die KI-Antwort dieses Assistenten angewendet werden.",
        ["spelling.assistant_section.unnamed"] = "(unbenannt)",
        ["spelling.assistant_section.manage"] = "Sets verwalten…",

        ["assistant.intensity.transform"] = "Umarbeitungstiefe (Transkript)",
        ["assistant.intensity.generate"] = "Freiheit der Generierung",
        ["assistant.intensity.answer"] = "Ausführlichkeit der Antwort",
        ["assistant.intensity.generic"] = "Intensität",
        ["assistant.writing_style.header"] = "Schreibstil",
        ["assistant.paragraphs.header"] = "Absätze und Leerzeilen",
        ["assistant.emojis.header"] = "Emojis und Social-Ton",
        ["assistant.instruction.placeholder"] = "Anweisung",

        ["tray.open"] = "öffnen",
        ["tray.active"] = "aktiv",
        ["tray.exit"] = "beenden",

        ["language.label"] = "Sprache",
        ["language.auto"] = "Systemstandard",
        ["language.english"] = "Englisch",
        ["language.german"] = "Deutsch",
        ["language.restart_hint"] = "Sprachänderungen wirken erst beim nächsten Start der App.",

        ["common.cancel"] = "Abbrechen",
        ["common.ok"] = "OK",
        ["common.update"] = "Aktualisieren",
        ["common.reset"] = "Zurücksetzen",
        ["common.note"] = "Hinweis",
        ["common.version"] = "Version",
        ["common.author"] = "Autor",
        ["common.architecture"] = "Architektur",
        ["common.tech"] = "Technik",
        ["common.assistant"] = "Assistent",
        ["common.time"] = "Zeit",
        ["common.insert_method"] = "Einfügemethode",
        ["common.via_clipboard"] = "über die Zwischenablage einfügen",
        ["common.directly_typed"] = "direkt tippen (zeichenweise)",

        ["status.ready"] = "Bereit",
        ["status.ready.hint"] = "Bereit. Halte ein Tastenkürzel gedrückt, um zu diktieren.",
        ["status.ready_to_dictate"] = "Bereit zum Diktieren",
        ["status.recording"] = "Aufnahme",
        ["status.processing"] = "Verarbeitung",
        ["status.recording_in_progress"] = "Aufnahme läuft",
        ["status.success"] = "Erfolgreich",
        ["status.error"] = "Fehler",
        ["status.inactive"] = "Inaktiv",
        ["status.setup_required"] = "Einrichtung erforderlich",
        ["status.setup_required.long"] = "Einrichtung erforderlich. Bitte prüfe die markierten Einstellungen.",
        ["status.please_check"] = "Bitte prüfe Anbieter, API-Schlüssel und Tastenkürzel der Assistenten.",
        ["status.api_key_needed"] = "Bitte einen API-Schlüssel für die KI-Verarbeitung eintragen oder speichern.",

        ["overview.intro"] =
            "Assistent für gesprochene Eingabe bei gedrückter Taste (Push-to-Talk): Transkription, KI-gestützte Textverarbeitung und Einfügen in aktive Anwendungen.",
        ["overview.feature_scope"] = "Funktionsumfang",
        ["overview.feature_scope.body"] =
            "Die App nimmt Sprache nur auf, solange ein Tastenkürzel gedrückt gehalten wird, transkribiert sie über den konfigurierten Cloud-Anbieter und verarbeitet den Text nach deiner Anweisung, dem Schreibstil, der Intensität und den Absatz-Optionen in der Assistenten-Karte.",
        ["overview.feature_scope.translation"] =
            "Eingabe- und Ausgabesprache können getrennt eingestellt werden, wodurch die Verarbeitung auch als Übersetzung genutzt werden kann.",
        ["overview.feature_scope.type"] =
            "Der Assistenten-Typ wählt nur die Quelle (Transkript, nur Anweisung oder Zwischenablage); Feinsteuerung erfolgt über die weiteren Felder pro Tastenkürzel.",
        ["overview.test_field"] = "Testfeld",
        ["overview.test_field.body"] =
            "Hier kannst du das Einfügen ausprobieren: Klicke ins Feld, halte ein Assistenten-Tastenkürzel gedrückt und sprich. Der finale Text wird in dieses Feld eingefügt.",
        ["overview.test_field.placeholder"] = "Testeinfügung schreibt nur in dieses Feld.",

        ["audio.input"] = "Audioeingabe",
        ["audio.input.intro"] = "Wähle das Mikrofon, das beim Sprechen bei gedrückter Taste verwendet wird.",
        ["audio.source"] = "Audioquelle",
        ["audio.refresh_devices"] = "Geräte aktualisieren",
        ["audio.devices_updated"] = "Audiogeräte wurden aktualisiert. Speichere die Einstellungen, um die Auswahl zu übernehmen.",
        ["language.section.title"] = "Sprache",
        ["language.section.intro"] =
            "Damit kann der Assistent zugleich übersetzen: Eingabe erkennen lassen und Ausgabe gezielt wählen.",
        ["language.input"] = "Eingabesprache",
        ["language.output"] = "Ausgabesprache",
        ["language.input.assistant"] = "Eingabesprache (Assistent)",
        ["language.output.assistant"] = "Ausgabesprache (Assistent)",

        ["pipeline.transcription"] = "Transkription",
        ["pipeline.ai_processing"] = "KI-Verarbeitung",
        ["pipeline.insertion"] = "Einfügen",
        ["pipeline.provider"] = "Anbieter",
        ["pipeline.model"] = "Modell",
        ["pipeline.api_key.stt"] = "API-Schlüssel für die Transkription",
        ["pipeline.api_key.llm"] = "API-Schlüssel für die KI-Verarbeitung",
        ["pipeline.api_key.placeholder"] = "OpenAI-API-Schlüssel (sk-…)",
        ["pipeline.api_key.saved_placeholder"] = "API-Schlüssel ist gespeichert. Zum Ersetzen neuen Schlüssel eingeben.",
        ["pipeline.api_key.new_placeholder"] = "Neuen API-Schlüssel eingeben",
        ["pipeline.api_key.saved_status"] = "API-Schlüssel gespeichert.",
        ["pipeline.api_key.missing_status"] = "Kein API-Schlüssel gespeichert.",
        ["pipeline.api_key.replace"] = "Schlüssel ersetzen",
        ["pipeline.api_key.note"] =
            "API-Schlüssel werden lokal für den aktuellen Windows-Benutzer per DPAPI verschlüsselt gespeichert.",
        ["pipeline.test_connection"] = "Verbindung prüfen",
        ["pipeline.connection_test"] = "Verbindungstest",
        ["pipeline.connection_test.prompt"] = "Antworte exakt mit OK.",
        ["pipeline.connection_test.success"] = "KI-Verbindung erfolgreich getestet.",
        ["pipeline.connection_test.failure"] = "KI-Verbindung fehlgeschlagen. Bitte prüfe API-Schlüssel und Modell.",
        ["pipeline.model.suggest_stt"] = "Vorschlag aus Liste oder eigene Modell-ID (z. B. gpt-4o-mini-transcribe)",
        ["pipeline.model.suggest_llm"] = "Vorschlag aus Liste oder eigene Modell-ID (z. B. gpt-5.1)",
        ["pipeline.retries"] = "Wiederholungsversuche bei Fehler (0–5)",
        ["pipeline.insert_method"] = "Einfügemethode",
        ["pipeline.restore_clipboard"] = "Zwischenablage nach dem Einfügen wiederherstellen",
        ["pipeline.custom_endpoint"] = "Eigener Endpoint (URL)",
        ["pipeline.custom_endpoint.placeholder_stt"] = "z. B. https://api.groq.com/openai/v1/audio/transcriptions",
        ["pipeline.custom_endpoint.placeholder_llm"] = "z. B. https://api.groq.com/openai/v1/chat/completions",
        ["validation.custom_endpoint_missing"] = "Bitte die Endpoint-URL eintragen.",
        ["general.launch_minimized"] = "beim Start in den Infobereich minimieren",
        ["general.minimize_to_tray"] = "beim Minimieren in den Infobereich verbergen",
        ["general.autostart"] = "mit Windows starten",
        ["assistant.system_prompt_override"] = "benutzerdefinierter System-Prompt",
        ["common.unknown"] = "unbekannt",

        ["assistants.intro"] =
            "Du kannst beliebig viele Assistenten anlegen. Der Typ legt nur fest, ob ein Transkript, nur eine Anweisung oder die Zwischenablage als Quelle dient; Stil, Intensität und Absätze steuerst du in der jeweiligen Karte.",
        ["assistants.add_new"] = "Neuen Assistenten anlegen",
        ["assistants.add_button"] = "+ Assistent hinzufügen",
        ["assistants.new_type"] = "Typ des neuen Assistenten",
        ["assistants.empty"] = "Noch keine Assistenten vorhanden. Lege oben einen an.",
        ["assistants.delete"] = "Assistent löschen",
        ["assistants.delete.confirm.title"] = "Assistent löschen?",
        ["assistants.drop_caption"] = "hier einsortieren",
        ["assistants.drag_handle"] = "ziehen, um zu sortieren",
        ["assistants.delete.confirm.body"] = "„{0}“ unwiderruflich aus der Liste entfernen?",
        ["assistants.display_name"] = "Anzeigename",
        ["assistants.assistant_type"] = "Assistenten-Typ",
        ["assistants.hotkey.capture"] = "Tastenkürzel aufnehmen",
        ["assistants.hotkey.prompt"] = "Tastenkombination drücken …",
        ["assistants.hotkey.aborted"] = "Aufnahme des Tastenkürzels abgebrochen.",
        ["assistants.hotkey.press_now"] = "Drücke jetzt die gewünschte Tastenkombination. Escape bricht ab.",
        ["assistants.hotkey.applied"] = "Tastenkürzel übernommen und aktiviert.",
        ["assistants.hotkey.applied_cleared"] = "Tastenkürzel übernommen. Bei {0} wurde die gleiche Kombination entfernt — jede Taste kann nur einem Assistenten zugeordnet sein.",
        ["assistants.hotkey.register_failed"] = "Dieses Tastenkürzel konnte nicht registriert werden. Bitte wähle eine andere Kombination.",
        ["assistants.template"] = "Vorlage",
        ["assistants.template.apply"] = "Vorgefertigte Anweisung in das Feld übernehmen",
        ["assistants.template.none"] = "Keine passenden Vorlagen",
        ["assistants.template.replace.title"] = "Anweisung ersetzen?",
        ["assistants.template.replace.body"] =
            "Der aktuelle Text im Anweisungs-Feld wird durch die Vorlage ersetzt. Fortfahren?",
        ["assistants.template.replace.button"] = "Ersetzen",

        ["general.startup"] = "Startverhalten und Grenzen",
        ["general.max_recording_seconds"] = "Maximale Aufnahmedauer",
        ["general.timeout_seconds"] = "Zeitlimit für die Verarbeitung (Sekunden)",
        ["general.play_sounds"] = "Hinweistöne bei Aufnahme-Start und -Ende abspielen",
        ["general.sound.volume"] = "Lautstärke der Hinweistöne",
        ["general.sound.volume.value"] = "{0} %",
        ["general.sound.test"] = "Test",
        ["general.reset"] = "Standard wiederherstellen",
        ["general.reset.intro"] =
            "Setzt alle Einstellungen auf die Standardwerte zurück. API-Schlüssel bleiben dabei erhalten.",
        ["general.reset.done"] = "Standardwerte wurden eingesetzt und gespeichert.",

        ["diagnostics.title"] = "Diagnose",
        ["diagnostics.history.title"] = "Verarbeitungsverlauf",
        ["diagnostics.history.intro"] =
            "Beim Aktivieren werden die letzten 5 erfolgreichen Verarbeitungen gespeichert (Zeit, Assistent, Transkript, KI-Antwort). Hilfreich zur Klärung, ob z. B. Absätze von der KI geliefert werden oder erst beim Einfügen verloren gehen. Beim Deaktivieren wird der Puffer sofort gelöscht.",
        ["diagnostics.history.clear_now"] = "Verlauf jetzt löschen",
        ["diagnostics.history.cleared"] = "Verarbeitungsverlauf gelöscht.",
        ["diagnostics.copy"] = "Kopieren (enthält ggf. Verlauf)",
        ["diagnostics.copy.success"] = "Diagnose wurde in die Zwischenablage kopiert.",
        ["diagnostics.copy.failure"] = "Diagnose konnte nicht kopiert werden.",
        ["diagnostics.open_log"] = "Logdatei öffnen",
        ["diagnostics.open_log.failure"] = "Logdatei konnte nicht geöffnet werden.",
        ["diagnostics.open"] = "Diagnose öffnen",
        ["diagnostics.history.latest_header"] = "Letzte Verarbeitungen (neueste zuerst, max. ",
        ["diagnostics.history.keep_label"] =
            "Verarbeitungsverlauf für die Diagnose speichern (max. 5 Einträge, enthält Transkripte und KI-Antworten)",

        ["about.copyright"] = "Copyright",
        ["about.license"] = "Lizenz",
        ["about.open_license"] = "Lizenz öffnen",
        ["about.license.body"] =
            "Schreibkraft wird als Open-Source-Software veröffentlicht. Du darfst die App verwenden, kopieren, verändern, weitergeben und eigene Varianten daraus erstellen.",
        ["about.license.body2"] =
            "Die Veröffentlichung erfolgt unter der MIT-Lizenz. Der Urheberrechtshinweis von Ronny Schulz und der Lizenztext müssen bei Weitergabe erhalten bleiben.",
        ["about.license.body3"] = "Die Software wird ohne Gewährleistung bereitgestellt.",
        ["about.license.not_found"] = "Lizenzdatei wurde im Installationsverzeichnis nicht gefunden.",
        ["about.license.open_failed"] = "Lizenzdatei konnte nicht geöffnet werden.",
        ["about.third_party.open"] = "Drittanbieter-Hinweise öffnen",
        ["about.third_party.not_found"] = "Drittanbieterhinweise wurden im Installationsverzeichnis nicht gefunden.",
        ["about.third_party.header"] = "Drittanbieter-Komponenten",
        ["about.third_party.description"] = "Liste aller eingebundenen NuGet-Pakete.",
        ["about.local_data.header"] = "Lokale Daten",
        ["about.local_data.description"] = "Pfade zu Settings und Logs.",
        ["about.third_party.open_failed"] = "Drittanbieterhinweise konnten nicht geöffnet werden.",
        ["about.privacy.title"] = "Datenschutz und Verarbeitung",
        ["about.privacy.body"] =
            "Für Transkription und KI-Verarbeitung werden Audio bzw. Text an den konfigurierten Anbieter übertragen. Welche Daten dort verarbeitet werden, richtet sich nach dessen Bedingungen.",
        ["about.privacy.body2"] =
            "Audiodaten, Transkripte und finale Texte werden standardmäßig nicht in Logs gespeichert. Logs enthalten technische Statusinformationen und Fehlerhinweise.",
        ["about.tech.intro"] = "WinUI 3 / Windows App SDK",
        ["about.tech.dependency"] = "Dienstorientiert mit Abhängigkeitsinjektion",
        ["about.tech.open_source"] = "Open-Source",
        ["about.tech.runtime"] = "Runtime",

        ["error.settings_save"] = "Einstellungen konnten nicht gespeichert werden: {0}",
        ["error.settings_save.access"] = "Einstellungen konnten nicht gespeichert werden, weil Windows den Zugriff verweigert. Pfad: {0}",
        ["error.settings_save.io"] = "Einstellungen konnten nicht gespeichert werden, weil die Datei gerade nicht verfügbar ist. Pfad: {0}",
        ["error.settings_save.path"] = "Einstellungen konnten nicht gespeichert werden, weil der Einstellungsordner nicht verfügbar ist. Pfad: {0}",
        ["error.settings_save.generic"] = "Einstellungen konnten nicht gespeichert werden. Pfad: {0}",
        ["error.page.open"] = "Seite konnte nicht geöffnet werden",
        ["error.page.open.body"] = "Die Seite „{0}“ konnte nicht geöffnet werden.",
        ["error.page.open_with_tag"] = "Beim Öffnen von „{0}“ ist ein Fehler aufgetreten.",
        ["error.config"] = "Konfiguration",

        ["diag.label.app_version"] = "App-Version",
        ["diag.label.assembly_version"] = "App-Version (Assembly)",
        ["diag.label.exe_product_version"] = "App-Version (Exe/Product)",
        ["diag.label.exe"] = "Exe",
        ["diag.label.net_runtime"] = ".NET-Laufzeit",
        ["diag.label.data_dir"] = "App-Datenpfad",
        ["diag.label.log_dir"] = "Protokolle",
        ["diag.label.stt_provider"] = "Transkriptionsanbieter",
        ["diag.label.stt_model"] = "Transkriptionsmodell",
        ["diag.label.audio_source"] = "Audioquelle",
        ["diag.label.input_lang"] = "Eingabesprache",
        ["diag.label.output_lang"] = "Ausgabesprache",
        ["diag.label.llm_provider"] = "KI-Anbieter",
        ["diag.label.llm_model"] = "KI-Modell",
        ["diag.label.hotkey_status"] = "Tastenkürzel-Status",
        ["diag.label.hotkey_status.check"] = "prüfen",
        ["diag.label.hotkey_status.valid"] = "gültig",
        ["diag.label.setup_status"] = "Einrichtungsstatus",
        ["diag.label.status"] = "Status",
        ["diag.unknown"] = "unbekannt",
        ["diag.elevated_note"] =
            "Hinweis: Einfügen in erhöhte Zielanwendungen kann scheitern, wenn Schreibkraft nicht ebenfalls erhöht läuft.",
        ["diag.history.empty"] = "Letzte Verarbeitungen: (noch keine seit Aktivierung)",
        ["diag.history.header"] = "Letzte Verarbeitungen (neueste zuerst, max. {0}):",
        ["diag.entry.time"] = "Zeit",
        ["diag.entry.assistant"] = "Assistent",
        ["diag.entry.insert_method"] = "Einfügemethode",
        ["diag.entry.timings"] = "Dauer",
        ["diag.entry.timings.value"] = "gesamt {0} | Aufnahme {1} | Transkription {2} | KI {3} | Einfügen {4}",
        ["diag.entry.timings.recorder_stop"] = "Recorder-Stopp",
        ["diag.entry.system_prompt"] = "System-Prompt (an die KI)",
        ["diag.entry.mode_prompt"] = "Mode-Prompt (an die KI, mit Anweisung/Quelltext)",
        ["diag.entry.transcript"] = "Transkript (an die KI als Nutzernachricht angehängt)",
        ["diag.entry.ai_response"] = "KI-Antwort (roh, vor dem Einfügen)",

        ["settings.saved.status"] = "Einstellungen gespeichert. Status={0}",
        ["error.no_recording_device"] = "Kein funktionierendes Aufnahmegerät verfügbar.",
        ["status.recording_release"] = "Aufnahme läuft … Loslassen, um zu verarbeiten.",
        ["status.inactive.long"] = "Inaktiv. Aktiviere die App im Tray-Menü, um Tastenkürzel zu nutzen.",
        ["error.mic_access"] = "Mikrofonzugriff fehlgeschlagen. Bitte prüfe Mikrofon und Windows-Datenschutzeinstellungen.",
        ["error.tray_window"] = "Tray-Fenster konnte nicht erstellt werden.",
        ["validation.stt_model"] = "Bitte ein Transkriptionsmodell wählen.",
        ["validation.llm_model"] = "Bitte ein KI-Modell wählen.",
        ["validation.stt_api_key"] = "Bitte einen API-Schlüssel für die Transkription speichern.",
        ["validation.llm_api_key"] = "Bitte einen API-Schlüssel für die KI-Verarbeitung speichern.",
        ["validation.stt_provider"] = "Bitte einen Transkriptionsanbieter auswählen.",
        ["validation.llm_provider"] = "Bitte einen KI-Anbieter auswählen.",
        ["validation.stt_unsupported"] = "Der gewählte Transkriptionsanbieter wird noch nicht unterstützt.",
        ["validation.llm_unsupported"] = "Der gewählte KI-Anbieter wird noch nicht unterstützt.",
        ["validation.no_assistants"] = "Es ist kein Assistent konfiguriert. Bitte mindestens einen anlegen.",
        ["hotkey.register_failed"] = "Globale Tastenkürzel konnten nicht registriert werden.",
        ["audio.recording_failed"] = "Audioaufnahme wurde mit Fehler beendet.",
        ["clipboard.locked"] = "Zwischenablage konnte nicht geöffnet werden.",
        ["clipboard.alloc_failed"] = "Zwischenablage-Speicher konnte nicht reserviert werden.",
        ["clipboard.lock_failed"] = "Zwischenablage-Speicher konnte nicht gesperrt werden.",
        ["clipboard.set_failed"] = "Zwischenablage konnte nicht gesetzt werden.",
        ["clipboard.not_expected"] = "Zwischenablage enthält nicht den vorgesehenen Text.",
        ["error.input_send"] = "Tastatureingabe konnte nicht gesendet werden ({0}/{1} Ereignisse, Win32-Fehler {2}). ",
        ["error.input_send.causes"] = "Mögliche Ursachen: Die Zielanwendung läuft mit höheren Rechten (UIPI blockiert Eingaben), ",
        ["error.input_send.middle"] = "die Eingabe wurde durch das Betriebssystem blockiert oder das Zielfenster akzeptiert kein SendInput. ",
        ["error.input_send.tip"] = "Tipp: Wechsle in den Einstellungen unter „Einfügen“ die Methode auf „über die Zwischenablage einfügen“.",
        ["mic.access_failed_long"] = "Mikrofonzugriff fehlgeschlagen. Bitte prüfe Mikrofon, Windows-Datenschutzeinstellungen und ob ein anderes Programm das Gerät blockiert.",
        ["hotkey.validation.need_modifier_and_key"] = "Bitte mindestens eine Zusatztaste und eine Haupttaste verwenden.",
        ["hotkey.validation.need_modifier"] = "Bitte mindestens eine Zusatztaste wie Strg, Alt, Umschalt oder Win verwenden.",
        ["hotkey.validation.need_main_key"] = "Bitte eine Haupttaste angeben.",
        ["pipeline.error.in_progress"] = "Eine Verarbeitung läuft bereits.",
        ["pipeline.error.setup_required"] = "Einrichtung erforderlich.",
        ["pipeline.error.assistant_not_found_user"] = "Assistent nicht gefunden. Bitte Tastenkürzel neu zuweisen.",
        ["pipeline.error.assistant_not_found_log"] = "Assistent mit Id '{0}' nicht in den Einstellungen gefunden.",
        ["pipeline.error.insert_failed_user"] = "Einfügen fehlgeschlagen. Details siehe Diagnose.",
        ["pipeline.error.insert_failed_log"] = "Einfügen fehlgeschlagen ({0}).",
        ["pipeline.error.clipboard_no_retries"] = "Einfügen über die Zwischenablage: keine Versuche konfiguriert.",
        ["pipeline.error.no_speech_user"] = "Keine Sprache erkannt. Es wurde nichts eingefügt.",
        ["pipeline.error.no_speech_log"] = "Keine Sprache erkannt.",
        ["pipeline.error.no_text_user"] = "Kein Text erkannt. Es wurde nichts eingefügt.",
        ["pipeline.error.no_final_text"] = "Kein finaler Text nach Transkription/KI.",
    };
}
