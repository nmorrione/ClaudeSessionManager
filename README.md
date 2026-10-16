# ClaudeSessionManager

Programma Windows (WPF, .NET 10) per **esportare e importare le sessioni di Claude Code** create con l'app desktop Claude (scheda *Code*).

Serve per:
- fare il **backup** delle sessioni in un posto sicuro (es. un repository privato);
- **passare una sessione a un collega**, che la ritrova nell'app Claude e può continuare a lavorarci con tutto il contesto.

## Dove Claude salva le sessioni

| Cosa | Percorso |
|---|---|
| Scheda della sessione (titolo, cartella, stato) | `%APPDATA%\Claude\claude-code-sessions\<account>\<org>\local_<id>.json` |
| Conversazione | `%USERPROFILE%\.claude\projects\<cartella codificata>\<cliId>.jsonl` |
| Subagent, titolo personalizzato, ... | `%USERPROFILE%\.claude\projects\<cartella codificata>\<cliId>\` |
| Memoria del progetto | `%USERPROFILE%\.claude\projects\<cartella codificata>\memory\` |
| Cronologia dei file (rewind) | `%USERPROFILE%\.claude\file-history\<cliId>\` |

La *cartella codificata* è il percorso del progetto con ogni carattere non alfanumerico sostituito da `-`
(es. `C:\Source\VS\c#\cruscotto` → `C--Source-VS-c--cruscotto`).

## Uso

### Esporta
1. Spunta le sessioni da esportare (filtro per titolo o cartella).
2. Scegli se includere la memoria di progetto e la cronologia dei file.
3. **Esporta selezionate...** → viene creato un unico `.zip`.

### Importa
1. **Apri archivio...** e scegli lo zip.
2. Per ogni sessione verifica la **cartella su questo PC**: se il progetto è in un percorso diverso, modificala
   (doppio clic sulla cella, oppure evidenzia più righe e usa *Scegli cartella...*).
   I percorsi dentro la conversazione vengono aggiornati automaticamente.
3. Scegli l'account dell'app di destinazione e premi **Importa selezionate**.
4. Se le sessioni non compaiono subito, chiudi e riapri l'app Claude.

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

Richiede .NET 10 SDK (Windows).
