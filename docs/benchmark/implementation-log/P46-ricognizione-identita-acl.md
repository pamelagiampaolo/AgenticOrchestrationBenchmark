# P46 — Ricognizione di identità Windows e ACL

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** valutazione delle opzioni di confinamento dopo P43–P45.
- **Stato:** ricognizione in sola lettura completata; identità dedicata e ACL
  restrittive non implementate né certificate.
- **Chiamate LLM:** nessuna; nessun contenuto di test riservati aperto.

Obiettivo: verificare se una diversa identità Windows possa essere considerata
una soluzione immediata, esaminando privilegi e ACL, senza cambiare l'host.
Componenti: token dell'utente osservato, comandi Windows e metadati ACL delle
directory fidate. Criteri: distinguere regole ACL da accesso effettivo,
descrivere i privilegi osservati e identificare i prerequisiti non verificati.

## Risultati osservati

- `whoami /priv`: nella prima sessione osservata compare soltanto
  `SeChangeNotifyPrivilege`, abilitato. Sono disponibili i comandi
  `New-LocalUser`, `Get-LocalUser`, `Start-Process`, `icacls.exe` e `runas.exe`;
  la presenza non dimostra il permesso di creare account o avviare un processo
  come altra identità.
- Con accesso autorizzato ai normali permessi dell'utente, `whoami /groups` ha
  riportato `BUILTIN\Users` e `NT AUTHORITY\Authenticated Users` abilitati;
  `BUILTIN\Administrators` era presente come gruppo utilizzabile soltanto per
  negazione. La verifica non ha creato un token di un utente dedicato.
- `Get-Acl` sulle directory evaluator, toolchain, run e risultati ha mostrato
  ACL leggibili e non protette dall'ereditarietà. Le ACL di `reserved` e dei
  39 file presenti al momento della prova sono state ispezionate **solo come
  metadati**: 39/39 presentano una regola `Allow` con `ReadData` per
  `BUILTIN\Users`; 39/39 una regola `Allow` con `WriteData` per
  `NT AUTHORITY\Authenticated Users`; nessuno dei 39 mostrava una regola
  esplicita `Deny` nella ACL restituita. Non sono stati aperti contenuti.
- La directory `reserved` era raggiungibile come metadato con i permessi
  osservati. Un controllo accessorio del percorso ipotetico `reserved\tests`
  ha restituito `ItemNotFoundException`: quel percorso non era necessario alla
  verifica e non è stato assunto come struttura effettiva.

Le regole osservate mostrano che **creare soltanto un altro account non basta**
a stabilire isolamento: i gruppi generici possono concedere accesso. Le ACL
non sono una prova completa di accesso effettivo: token dell'eventuale nuovo
account, ACL di ogni directory antenata, altre restrizioni e modalità di avvio
devono essere verificati. Non si conclude da questi metadati che un pilota abbia
letto o modificato test riservati.

## Conseguenze per la soluzione

Un'identità dedicata richiederebbe, prima dell'uso sperimentale:

1. creazione e gestione sicura dell'identità e del relativo processo;
2. una policy esplicita per directory fidate, workspace, worker, toolchain e
   risultati, compresi i permessi ereditati dai gruppi generici;
3. controllo dell'accesso a rete, processi figli, credenziali e servizi locali;
4. prove positive e negative con la toolchain reale e sentinelle sintetiche,
   inclusi test agente e codice del workspace caricato dai test pubblici;
5. verifica dei processi e dei residui, oltre alla protezione della CLI e degli
   altri percorsi, senza trattare il solo worker come prova dell'intero sistema.

Tali interventi comportano modifiche di sistema e un cambiamento architetturale
significativo. Non sono stati creati account, cambiati gruppi o ACL, installati
strumenti né eseguiti test con una seconda identità. Non si assume che questa
alternativa sia più rapida o più sicura di HCS senza una prova controllata.

Le run ufficiali restano ferme per P43. Gli input congelati non sono stati
modificati e questa ricognizione non richiede da sola nuovi piloti. La scelta e
l'eventuale correzione richiedono analisi d'impatto B22 prima di decidere se
ripetere i sei piloti.

Il primo controllo testuale del documento è fallito perché cercava una frase
con maiuscole e spazi diversi da quelli presenti; il controllo corretto è stato
ripetuto. Nessun file riservato è stato aperto da quel controllo.
