# P14 - Ricognizione minima HCS

## Data e fase

- Data: 2026-11-08
- Fase: verifica di fattibilita del worker confinato

## Obiettivo

Verificare se Host Compute Service (HCS) consente di avviare rapidamente un
worker Windows confinato usando esclusivamente componenti gia disponibili
sull'host, senza installazioni, modifiche di sistema o creazione di risorse
persistenti.

## Componenti osservati

- servizi Windows `hns` e `vmcompute`;
- strumento diagnostico `hcsdiag.exe`;
- runtime container disponibili nel `PATH`;
- directory locali comunemente usate per immagini e layer Windows;
- gruppi di sicurezza dell'identita corrente.

## Criteri di accettazione

La prova e positiva soltanto se l'identita corrente puo accedere a HCS e sono
gia disponibili un runtime e un'immagine o layer Windows utilizzabili per una
prova usa e getta. In alternativa, la prova deve identificare un impedimento
riproducibile e concreto.

## Verifiche eseguite e risultati

### Servizi e strumenti

`Get-Service hns,vmcompute` ha restituito entrambi i servizi nello stato
`Running`. `Get-Command` ha trovato `hcsdiag.exe`, ma non ha trovato `docker`,
`podman`, `containerd`, `ctr` o `nerdctl`.

### Accesso a HCS

`hcsdiag list` ha rifiutato l'operazione con il messaggio:

> Privilegi insufficienti. Solo agli amministratori o agli utenti membri del
> gruppo utenti Amministratori Hyper-V e consentito accedere ai contenitori o
> alle macchine virtuali.

`whoami /groups` conferma che l'identita corrente appartiene a
`PAMPAM\CodexSandboxUsers` e ai normali gruppi utente, ma non ad Administrators
o Hyper-V Administrators.

### Immagini e layer locali

Non risultano presenti le directory:

- `C:\ProgramData\Microsoft\Windows\Images`;
- `C:\ProgramData\docker\windowsfilter`;
- `C:\ProgramData\containerd`.

La lettura dello stato delle feature tramite DISM richiede a sua volta un
processo elevato ed e terminata con errore 740. Questa informazione non e
necessaria per la conclusione: l'accesso HCS e gia negato e non sono disponibili
runtime o immagini locali.

## Esito

La prova e negativa per l'host e l'identita correnti. HCS non offre un percorso
semplice e immediato per il worker: prima di eseguire anche un prototipo servono
almeno un intervento amministrativo sui privilegi, un runtime o launcher HCS e
un'immagine Windows compatibile.

HCS resta tecnicamente una possibile architettura futura, ma adottarlo ora
introdurrebbe prerequisiti di macchina e gestione delle immagini che devono
essere congelati e replicati per tutte le condizioni sperimentali. Non e stata
effettuata alcuna modifica al sistema.

## Lavoro rimanente

La scelta piu semplice per proseguire senza container e mantenere il Job Object
gia provato e confinare il processo sotto un'identita Windows dedicata con ACL
minime e blocco di rete applicato all'identita o all'eseguibile. Questa
alternativa richiede una distinta iterazione e una prova di accettazione; non e
certificata da P14.

