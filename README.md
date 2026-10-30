# ClaudeSessionManager

<img src="tools/icon-preview.png" width="96" align="right" alt="Icona" />

Programma per Windows e Mac (Avalonia, .NET 10) per **esportare e importare le sessioni di Claude Code** create con l'app desktop Claude (scheda *Code*).

Serve per:
- fare il **backup** delle sessioni in un posto sicuro (es. un repository privato);
- **passare una sessione a un collega**, che la ritrova nell'app Claude e può continuare a lavorarci con tutto il contesto,
  anche se lui usa un Mac e tu Windows (o viceversa).

## Download

Scarica l'ultima versione dalla pagina [Releases](https://github.com/nmorrione/ClaudeSessionManager/releases/latest):

| File | Quando usarlo |
|---|---|
| `ClaudeSessionManager-x.y.z-win-x64.exe` | Windows 10/11 a 64 bit, senza installare nulla (~60 MB). |
| `ClaudeSessionManager-x.y.z-win-x64-net10.exe` | Windows, versione leggera: richiede il [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). |
| `ClaudeSessionManager-x.y.z-macOS-AppleSilicon.zip` | Mac con chip Apple (M1 o successivi), macOS 14 o più recente. |

**Windows:** basta avviare l'eseguibile. SmartScreen potrebbe chiedere conferma la prima volta, perché non è firmato digitalmente.

**Mac:** estrai lo zip e sposta *ClaudeSessionManager* in Applicazioni. L'app ha solo una firma ad-hoc, quindi al primo
avvio macOS la blocca: apri *Impostazioni di Sistema → Privacy e sicurezza* e premi *Apri comunque*, oppure da Terminale:

```
xattr -dr com.apple.quarantine /Applications/ClaudeSessionManager.app
```

## Dove Claude salva le sessioni

| Cosa | Windows | Mac |
|---|---|---|
| Scheda della sessione (titolo, cartella, stato) | `%LOCALAPPDATA%\Claude-Data\claude_<id>\claude-code-sessions\<account>\<org>\local_<id>.json` | `~/Library/Application Support/Claude/claude-code-sessions/<account>/<org>/local_<id>.json` |
| Conversazione | `%USERPROFILE%\.claude\projects\<cartella codificata>\<cliId>.jsonl` | `~/.claude/projects/<cartella codificata>/<cliId>.jsonl` |
| Subagent, titolo personalizzato, ... | `...\.claude\projects\<cartella codificata>\<cliId>\` | `~/.claude/projects/<cartella codificata>/<cliId>/` |
| Memoria del progetto | `...\.claude\projects\<cartella codificata>\memory\` | `~/.claude/projects/<cartella codificata>/memory/` |
| Cronologia dei file (rewind) | `%USERPROFILE%\.claude\file-history\<cliId>\` | `~/.claude/file-history/<cliId>/` |

La cartella delle schede è cambiata più volte con gli aggiornamenti dell'app Claude; il programma le cerca tutte:
- `%LOCALAPPDATA%\Claude-Data\claude_<id>\claude-code-sessions` (versioni recenti dell'app su Windows);
- `%LOCALAPPDATA%\Packages\Claude_<id>\LocalCache\Roaming\Claude\claude-code-sessions` (app da Microsoft Store, versioni precedenti);
- `%APPDATA%\Claude\claude-code-sessions` (installazione classica);
- su Mac `~/Library/Application Support/Claude-Data/*/claude-code-sessions` e `~/Library/Application Support/Claude/claude-code-sessions`.

La *cartella codificata* è il percorso del progetto con ogni carattere non alfanumerico sostituito da `-`
(es. `C:\Source\VS\c#\cruscotto` → `C--Source-VS-c--cruscotto`, `/Users/mario/progetto` → `-Users-mario-progetto`).

## Uso

### Esporta
1. Spunta le sessioni da esportare (filtro per titolo o cartella).
2. Scegli se includere la memoria di progetto e la cronologia dei file.
3. **Esporta selezionate...** → viene creato un unico `.zip`.

### Importa
1. **Apri archivio...** e scegli lo zip.
2. Per ogni sessione verifica la **cartella su questo computer**: se il progetto è in un percorso diverso, modificala
   (doppio clic sulla cella, oppure evidenzia più righe e usa *Scegli cartella...*).
   I percorsi dentro la conversazione vengono aggiornati automaticamente, anche fra Windows e Mac.
3. Scegli l'account dell'app di destinazione e premi **Importa selezionate**.
4. Se le sessioni non compaiono subito, chiudi e riapri l'app Claude.

### Riga di comando
`ClaudeSessionManager --list` elenca le sessioni trovate e le cartelle in cui le ha cercate (utile per capire
perché una sessione non compare).

## Formato dello zip

```
manifest.json                         elenco sessioni + dati per reimportarle
LEGGIMI.txt                           elenco leggibile
sessions/<localId>/meta.json          scheda per l'app desktop
sessions/<localId>/transcript.jsonl   conversazione
sessions/<localId>/extra/...          subagent, titolo, ecc.
sessions/<localId>/file-history/...   (opzionale) cronologia file
memory/<cartella codificata>/...      (opzionale) memoria di progetto
```

## Avvertenze

- Gli archivi contengono **tutto** ciò che è passato nelle sessioni: contenuto dei file letti, output dei comandi,
  eventuali password o stringhe di connessione. Conservali solo in luoghi **privati**.
- GitHub rifiuta file oltre 100 MB: per sessioni molto grandi esporta zip separati o usa Git LFS.
- Prima di sovrascrivere una sessione già presente, chiudila nell'app Claude.
- Il formato dei file di Claude non è documentato ufficialmente e potrebbe cambiare con gli aggiornamenti dell'app.

## Compilazione

```
dotnet build ClaudeSessionManager.sln -c Release
```

Richiede .NET 10 SDK. Le release le compila GitHub Actions ([build.yml](.github/workflows/build.yml)):
basta creare e inviare un tag `vX.Y.Z` e GitHub prepara l'exe Windows e l'app Mac, li prova e pubblica la Release.
Da *Actions → Build → Run workflow* si può anche lanciare la compilazione a mano, senza creare la Release.

Le icone (`icon.ico`, `AppIcon.icns`, `icon.png` in `ClaudeSessionManager\Assets`) si rigenerano su Windows con
`powershell -STA -ExecutionPolicy Bypass -File tools\make-icon.ps1`.
