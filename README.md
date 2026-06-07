# Schreibkraft

Schreibkraft ist eine Windows-Desktop-App für gesprochene Eingabe bei gedrückter Taste (Push-to-Talk). Die App nimmt Audio lokal auf, transkribiert es über den konfigurierten Anbieter, verarbeitet den Text per KI und fügt das Ergebnis in die aktive Anwendung ein. Du kannst beliebig viele Assistenten mit eigenem Typ, Tastenkürzel, Anweisung, Schreibstil, Intensität, Sprache und Formatierung anlegen.

**Aktuelle Version:** 1.2.1

**Hinweis:** Die App und der zugehörige Quellcode wurden wesentlich mit KI-gestützter Entwicklung (z. B. Assistent in der IDE) erstellt und von Menschen geprüft und freigegeben.

![Schreibkraft - Überblick](Screenshot.png)

## Voraussetzungen

- Windows 10/11 x64
- .NET SDK 10.0.202 oder neuer
- Visual Studio mit .NET Desktop Development und Windows App SDK-Unterstützung
- Mikrofonzugriff in den Windows-Datenschutzeinstellungen
- API-Schlüssel für die genutzten Cloud-Anbieter, lokale Anbieter wie Ollama oder LM Studio ausgenommen

## Start in Visual Studio

1. `Schreibkraft.sln` öffnen.
2. `Schreibkraft` als Startprojekt wählen.
3. Konfiguration `Debug|x64` verwenden.
4. NuGet-Restore abwarten und mit F5 starten.

Beim ersten Start übernimmt Schreibkraft fehlende Dateien aus dem früheren lokalen Datenordner nach `%LOCALAPPDATA%\Schreibkraft`, ohne bereits vorhandene Schreibkraft-Dateien zu überschreiben. Fehlen danach weiterhin Settings, werden `%LOCALAPPDATA%\Schreibkraft\settings.json` und `%LOCALAPPDATA%\Schreibkraft\logs` erzeugt sowie die drei Standard-Assistenten angelegt. Fehlen Pflichtwerte, öffnet die App das Einstellungsfenster mit dem Status `Einrichtung erforderlich`.

## Einstellungen

Die Einstellungen sind in diese Bereiche gegliedert:

- `Übersicht`: Funktionsüberblick und Testfeld für das Einfügen.
- `Assistenten`: Assistenten, Tastenkürzel, Typen, Anweisungen, Vorlagen, Sprach-Overrides, Schreibstil, Intensität, Absätze, Emojis und optionaler System-Prompt.
- `Rechtschreibkorrektur`: Wiederverwendbare Sets für exakte Wort-Ersetzungen und Eigennamen/Fachbegriffe.
- `Verarbeitung`: Transkription, KI-Verarbeitung, Anbieter, Modelle, API-Schlüssel, eigene Endpoints, Verbindungstest, Einfügemethode und Wiederholungsversuche.
- `Audio & Sprache`: Mikrofon, Eingabesprache und Ausgabesprache.
- `Allgemein`: Startverhalten, Zeitlimits, Hinweistöne und Standardwerte.
- `Diagnose`: technische Diagnose, Logdatei und optionaler Verarbeitungsverlauf.
- `Über`: Version, Lizenz, Datenschutz, lokale Daten und Drittanbieterhinweise.

## Assistenten und Typen

Schreibkraft kennt drei Assistenten-Typen. Der Typ legt nur die Quelle fest; Feineinstellungen erfolgen in der jeweiligen Assistenten-Karte.

| Typ | Sprachinput wird verstanden als | Quelle |
|---|---|---|
| Text bearbeiten | gesprochener Text, der korrigiert, geglättet oder umformuliert werden soll | nur Sprache |
| Text generieren | Anweisung, aus der die KI einen neuen Text erzeugt | nur Sprache |
| Antwort (Zwischenablage) | Anweisung, mit der die KI auf den Zwischenablage-Text antwortet | Sprache + Zwischenablage |

Standard-Tastenkürzel beim ersten Start: `Ctrl+Shift+1` bis `Ctrl+Shift+3`. Du kannst sie pro Assistent frei vergeben. Wird ein Tastenkürzel doppelt vergeben, entfernt Schreibkraft die gleiche Kombination beim bisherigen Assistenten, damit eine Kombination nur einmal aktiv ist.

Für Assistenten gibt es Vorlagen wie Korrektur, Glätten, klarer formulieren, freie Texterzeugung und Antwort auf Quelltext. Zusätzlich kannst du pro Assistent die globale Eingabe- oder Ausgabesprache überschreiben und bei Bedarf einen eigenen System-Prompt aktivieren.

## Bedienung

Schreibkraft läuft primär im Infobereich. Das Tray-Menü enthält `öffnen`, `aktiv` und `beenden`. Ist `aktiv` nicht angehakt, starten keine neuen Aufnahmen bei gedrückter Taste.

Tastenkürzel gedrückt halten, sprechen, loslassen. Die App stoppt die Aufnahme, transkribiert, verarbeitet und fügt den Text standardmäßig per **direktem Tippen (SendInput)** ein. In `Verarbeitung` kann die Einfügemethode auf **über die Zwischenablage einfügen** umgestellt werden. Beim Typ `Antwort (Zwischenablage)` wird der Zwischenablage-Inhalt zum Zeitpunkt des Hotkey-Drucks als Quelltext mitgegeben; ist die Zwischenablage leer, startet keine Aufnahme.

Bei Fehlern können Wiederholungsversuche für Transkription, KI-Verarbeitung und Zwischenablage-Einfügen konfiguriert werden. Schlägt die KI-Verarbeitung nach den Versuchen fehl, wird das Transkript eingefügt. Schlägt das Einfügen über die Zwischenablage fehl, kann Schreibkraft auf direktes Tippen zurückfallen.

## Anbieter

Für Transkription sind aktuell OpenAI, Groq, Deepgram, Azure Speech, AssemblyAI, ElevenLabs Scribe und OpenAI-kompatible Endpoints vorgesehen. Für KI-Verarbeitung sind OpenAI, Anthropic, Google Gemini, Groq, DeepSeek, xAI Grok, Ollama, LM Studio und OpenAI-kompatible Endpoints vorgesehen.

Bekannte Anbieter haben Standard-Endpoints. Für OpenAI-kompatible, lokale oder abweichende Installationen kann in `Verarbeitung` ein eigener Endpoint eingetragen werden. Modellfelder sind editierbar: Die App bietet Vorschläge an, erlaubt aber eigene Modell-IDs.

## Rechtschreibkorrektur

In `Rechtschreibkorrektur` kannst du Sets mit exakten Wort-Ersetzungen und Eigennamen/Fachbegriffen anlegen. Ein Set kann in beliebigen Assistenten aktiviert werden.

- Wort-Ersetzungen werden nach der KI-Antwort auf ganze Wörter angewendet.
- Eigennamen und Fachbegriffe werden der KI als korrekte Schreibweise mitgegeben, damit phonetisch ähnliche Fehltranskripte korrigiert werden können.

## Datenschutz

Standardmäßig werden keine Audiodaten, Transkripte oder finalen Texte protokolliert. Protokolle enthalten technische Statusinformationen und Fehlerhinweise. API-Schlüssel werden unter Windows per DPAPI für den aktuellen Benutzer verschlüsselt.

Audio wird zur Transkription an den konfigurierten Anbieter gesendet. Transkripte werden zur KI-Verarbeitung an den konfigurierten KI-Anbieter gesendet. Beim Typ `Antwort (Zwischenablage)` wird zusätzlich der Inhalt der Zwischenablage zum Zeitpunkt des Hotkey-Drucks an den KI-Anbieter gesendet. Der Diagnosebereich kann optional die letzten fünf erfolgreichen Verarbeitungen speichern; diese Funktion ist standardmäßig aus.

## Build und Tests

```powershell
dotnet restore
dotnet build .\Schreibkraft.sln -c Debug
dotnet test .\Schreibkraft.sln -c Debug
.\Build.ps1
```

`Build.ps1` veröffentlicht standardmäßig `Release|x64`. Mit `-Configuration Debug`, `-Platform x86|x64|arm64`, `-SkipInstaller` und `-Clean` kann der Build angepasst werden.

## Installer

Empfohlener Nutzerweg: `.\Build.ps1` erzeugt den App-Ordner unter **`artifacts\publish\Schreibkraft`**. Wenn Inno Setup installiert ist, entsteht zusätzlich **`artifacts\installer\Schreibkraft-Setup-<Version>.exe`**. Diese Setup-Datei installiert Schreibkraft nach `%LOCALAPPDATA%\Programs\Schreibkraft`, legt den Startmenüeintrag an und registriert die App normal in den Windows-Einstellungen unter `Installierte Apps`, inklusive Deinstallation.

```powershell
winget install JRSoftware.InnoSetup
.\Build.ps1
```

Der Setup-Installer prüft beim Installieren **Windows App Runtime 1.8 (x64)** und **.NET 10 Windows Desktop Runtime 10.0.7**. Fehlen sie, versucht er die Installation per **winget**; ohne winget werden die Microsoft-Installer direkt geladen. Die Runtime-Installer können **UAC** anzeigen.

Der bisherige Skriptweg bleibt als Entwickler- und Fallback-Variante erhalten: `.\Install.ps1` kopiert den Build nach `%LOCALAPPDATA%\Programs\Schreibkraft`, `.\Uninstall.ps1` entfernt diese Skriptinstallation; `-RemoveUserData` entfernt zusätzlich `%LOCALAPPDATA%\Schreibkraft` (Settings + Logs). Für normale Weitergabe ist die Setup-Datei vorzuziehen, weil sie in den Windows-Einstellungen sichtbar und dort deinstallierbar ist.

## GitHub-Sync und Release

`Sync-GitHub.ps1` synchronisiert den lokalen Branch mit GitHub. Bei `Push` und `PullPush` committed das Skript lokale Änderungen automatisch vor dem Push, wenn welche vorhanden sind.

```powershell
.\Sync-GitHub.ps1
.\Sync-GitHub.ps1 -CommitMessage "Beschreibung der Änderung"
.\Sync-GitHub.ps1 -NoAutoCommit
```

Für GitHub-Veröffentlichungen wird die Setup-Datei als Release-Asset hochgeladen:

```powershell
.\Build.ps1
.\Sync-GitHub.ps1 -Release
```

Das nutzt die `AppVersion` aus `Directory.Build.props`, erstellt oder aktualisiert den Tag `v<AppVersion>` und lädt `artifacts\installer\Schreibkraft-Setup-<Version>.exe` in das GitHub Release hoch. Das Skript fragt `Draft?` und `Pre-Release?` direkt mit ja/nein ab. Voraussetzung: GitHub CLI (`gh`) ist installiert und angemeldet; falls sie fehlt, versucht das Skript die Installation.

## Bekannte Einschränkungen

- Ein echter STT-Rundlauf muss manuell mit Mikrofon und API-Schlüssel geprüft werden.
- Einfügen in erhöhte Zielanwendungen kann scheitern, wenn Schreibkraft nicht mit denselben Rechten läuft.
- Ob eine Zielanwendung Einfügen aus der Zwischenablage wirklich übernommen hat, lässt sich ohne UI-Prüfung nicht sicher erkennen.
- Die App erzeugt statische Tray-Icons lokal im Projekt; MSIX-Packaging und Signierung sind noch nicht umgesetzt.
- Solution-Datei `Schreibkraft.sln`, Bibliotheken `Schreibkraft.Core` und `Schreibkraft.Infrastructure`.
