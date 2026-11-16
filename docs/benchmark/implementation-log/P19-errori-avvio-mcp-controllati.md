# P19 - Errori di autorizzazione MCP controllati

## Data e obiettivo

- Data: 2026-11-08.
- Obiettivo: impedire che un errore di accesso durante l'inizializzazione del
  server MCP diventi un'eccezione CLR non gestita e attivi il debugger
  Just-In-Time di Windows.

## Problema osservato

Durante i primi tentativi P18, un processo `dotnet.exe` ha attraversato una
directory sotto `%TEMP%` per la quale l'identità corrente non poteva leggere
`C:\Users\Utente\AppData`. `UnauthorizedAccessException` non faceva parte degli
errori di configurazione gestiti dal blocco di avvio. Windows ha quindi mostrato
il codice CLR `0xe0434352` e il tentativo fallito di avviare Visual Studio
Just-In-Time Debugger.

Non è necessario installare o abilitare un debugger: il server deve terminare in
modo controllato.

## Correzione

`StartupExceptionPolicy` classifica come errori di configurazione:

- argomenti o percorsi non validi;
- file o directory mancanti;
- accesso non autorizzato;
- errori di I/O.

Il confine di avvio stampa una sola diagnostica su stderr e restituisce exit code
2. Eccezioni inattese, come `InvalidOperationException`, non vengono nascoste.

## Verifiche osservate

- riproduzione con root sotto `%TEMP%`: exit code 2;
- messaggio: `Configurazione non valida: Access to the path ... is denied`;
- nessuno stack trace o popup JIT;
- test mirati della policy: 2/2;
- build: 0 avvisi, 0 errori;
- suite .NET completa: 73/73;
- probe P18: 27/27, audit singolo, worker vuota e stderr vuoto.

La correzione non modifica le autorizzazioni del sistema e non abilita il
debugger Just-In-Time.
