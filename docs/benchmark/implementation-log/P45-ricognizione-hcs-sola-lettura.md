# P45 — Ricognizione HCS in sola lettura

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** valutazione delle opzioni di confinamento dopo P43/P44.
- **Stato:** ricognizione completata; fattibilità HCS non dimostrata.
- **Chiamate LLM:** nessuna.

Obiettivo: verificare strumenti, servizi, accesso e immagini già visibili,
senza creare container, account, ACL o servizi. Componenti: `hcsdiag`, servizi
Windows, percorsi di immagini e strumenti container. Criteri: riportare output
e limiti osservati senza confondere la presenza di un servizio con un confine
funzionante o con la disponibilità della toolchain nel container.

## Risultati osservati

- `Get-Service vmcompute,hns`: entrambi `Running`, avvio `Manual`.
- `Get-Command`: `hcsdiag.exe` e `wsl.exe` presenti; `docker.exe`, `podman.exe`
  e `nerdctl.exe` non trovati nel PATH del controllo.
- `hcsdiag.exe help`: descrive operazioni di diagnostica su container/VM già
  esistenti; non mostra un comando di creazione. Il comando ha restituito exit
  code 1 pur stampando l'aiuto.
- `hcsdiag.exe list -raw`: accesso negato per privilegi insufficienti, con
  indicazione che sono necessari amministratori o membri del gruppo
  amministratori Hyper-V; exit code riportato `-2143878885`. Stesso risultato
  anche ripetendo con accesso autorizzato ai permessi normali dell'utente.
- `C:\ProgramData\Microsoft\Windows\Containers` contiene le directory
  `BaseImages`, `ContainerStorages`, `Dumps`, `Layers` e `Snapshots`;
  `BaseImages` ha zero voci visibili anche con accesso autorizzato. Questo non
  dimostra l'assenza di immagini registrate o memorizzate altrove.
- Le directory convenzionali `C:\ProgramData\docker`,
  `C:\ProgramData\containerd` e `C:\Program Files\containerd` non sono state
  trovate o non erano accessibili al controllo; non dimostrano l'assenza di
  installazioni in altri percorsi.
- `wsl.exe --list --verbose`: nella sandbox iniziale accesso negato; con accesso
  autorizzato il comando ha riportato nessuna distribuzione installata per il
  contesto osservato. Questo risultato non prova nulla sulla sicurezza di WSL.
- `Get-WindowsOptionalFeature` per `Containers` e `Microsoft-Hyper-V-All`:
  entrambi hanno richiesto privilegi Windows elevati. Il primo tentativo con due
  nomi in un solo parametro è fallito per tipo non valido; la ripetizione per
  singola funzionalità ha mostrato il limite di privilegi. Lo stato effettivo
  delle funzionalità non è stato accertato.

## Limiti e conseguenze

L'account osservato non ha potuto elencare i sistemi HCS; la ricognizione non
dimostra se esistano container, immagini altrove o un runtime utilizzabile con
altri privilegi. Non sono stati installati strumenti, creati container o
modificati gruppi, servizi, ACL o funzionalità. Windows Sandbox non è stata
usata o introdotta.

HCS rimane un'opzione architetturale, ma non è pronta all'uso in questo stato
osservato. Prima di sceglierla occorrerebbe chiarire disponibilità e gestione
di privilegi, runtime e immagine e poi provare isolamento e compatibilità;
operazioni di installazione o modifica del sistema richiedono una decisione
esplicita e una valutazione delle alternative. Nessuna run ufficiale deve
iniziare finché la falla P43 e gli altri requisiti aperti non sono risolti.

Questa ricognizione non modifica gli input congelati e non richiede da sola di
ripetere i piloti. L'impatto di un'eventuale correzione resta soggetto a B22.
