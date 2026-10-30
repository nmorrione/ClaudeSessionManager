**Claude Session Manager** esporta e importa le sessioni di Claude Code dell'app desktop Claude, su Windows e su Mac.

### Download
| File | Note |
|---|---|
| `ClaudeSessionManager-x.y.z-win-x64.exe` | Windows 10/11 a 64 bit, non richiede nulla (~60 MB). |
| `ClaudeSessionManager-x.y.z-win-x64-net10.exe` | Windows, versione leggera: richiede il [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). |
| `ClaudeSessionManager-x.y.z-macOS-AppleSilicon.zip` | Mac con chip Apple (M1 o successivi), macOS 14 o più recente. |

**Windows:** l'eseguibile non è firmato; al primo avvio SmartScreen può chiedere conferma (*Ulteriori informazioni → Esegui comunque*).

**Mac:** estrai lo zip e sposta *ClaudeSessionManager* in Applicazioni. L'app ha solo una firma ad-hoc, quindi al primo avvio
macOS la blocca: apri *Impostazioni di Sistema → Privacy e sicurezza* e premi *Apri comunque*, oppure da Terminale:
`xattr -dr com.apple.quarantine /Applications/ClaudeSessionManager.app`

Le sessioni esportate su un sistema si possono importare sull'altro: basta indicare, per ogni sessione, la cartella del progetto sul computer di destinazione.

> Gli archivi esportati contengono tutto ciò che è passato nelle sessioni: conservali solo in luoghi privati.
