# P35 — Identità evaluator e verifica degli input congelati

## Classificazione e obiettivo

- **Data:** 18 novembre 2026.
- **Fase:** freeze precedente ai nuovi piloti.
- **Stato:** identità dell'evaluator registrata; verifiche degli input descritti
  sotto completate. Restano le verifiche specifiche delle singole run.
- **Chiamate LLM:** nessuna.

L'iterazione registra il commit dell'evaluator e confronta gli input disponibili
con le identità documentate. I criteri sono: repository evaluator pulito al
commit indicato; manifest e file confrontati senza differenze; eventuali limiti o
errori esplicitati. Non modifica l'harness o la baseline sperimentale.

## Identità

- Commit sorgente sperimentale benchmark e harness:
  `aee8d1162bb496895990a00095cc14c2651ff752`.
- Tree sorgente: `7543e5dddc103dfbc7250566c26b765c370fbda0`.
- Commit evaluator per i nuovi piloti e le successive run:
  `a2f1626a50e696d2930bfc72d28b6cb339dc9d9e`.
- Commit benchmark corrente alla verifica:
  `d7c0369ec05edda85b5c6b938c067782c8734af6`; contiene il lavoro di freeze
  successivo e non sostituisce il commit sorgente sperimentale.

`git status --porcelain=v1 --untracked-files=all` nell'evaluator non ha
restituito file modificati o non tracciati. `git log -1` ha restituito il commit
evaluator indicato sopra. Il benchmark era pulito prima di questo documento.

## Verifiche eseguite e risultati

- `New-FrozenSourceManifest.ps1 -Commit aee8d1162bb496895990a00095cc14c2651ff752`
  con output temporaneo: 88 file; aggregato
  `82dc5305d6a268f12342b0e736778b78dd4f0f3d4e59a7944d95128fe9fc1836`;
  SHA-256 JSON `5e1941cc35d0be9e09841a331d36bfd38d381f3edbdc9bd3b95be58812a9edcc`,
  identico al manifest P30. Il file temporaneo è stato rimosso.
- Confronto tramite `Get-FileHash` e dimensioni dei file nel manifest sorgente
  con la baseline fidata: 19 file, zero differenze.
- Confronto tramite `Get-FileHash` e dimensioni di tutti i file nei manifest
  delle copie definitive: harness 16/16 e toolchain 1148/1148, zero differenze;
  conteggio dei file effettivi rispettivamente 16 e 1148. SHA-256 dei manifest:
  harness `2ae6e4d9c69e4d54e2430f574b7bea58654d07eca469a853e3f54d5b3db40b2c`,
  toolchain `b2644f8c0500e13f8b80da2828bd3b18f3a5423f5c394df1fcc2b641ca424231`.
- `Get-FileHash` della CLI: eseguibile
  `6099672ebe5dcb518b67d1d32dde1e3360bc6bee1dae7f8d28a69c49acc7c865`;
  manifest `bf8e333af1551fd5fcd640c6311f752a4091054a405a57f26cf0522784ea1d0d`.
  `claude.exe --version` ha restituito `2.1.312 (Claude Code)`.
- `Get-FileHash` nell'evaluator: manifest pubblico
  `dc5f7dbb2d4e8d8811e6e5335391f76199b7b1c1c1ba0635059672317abc7e5b`,
  manifest riservato
  `ce05f45cb2e93ee0e499abd86102cf8e8c2505f7029c83b099c8ddeecb8bad09`,
  schema `4ddc434c343db376516db44423e33c1e3da1d9f124e158c5d57a135366f537bf`;
  corrispondono al documento di freeze dell'evaluator.
- `evaluator/Test-PublicManifest.ps1` con baseline fidata e toolchain congelata:
  exit code 0, `Manifest coerente (test verificati: True)`.
- `evaluator/Test-ReservedManifest.ps1` con toolchain congelata: exit code 0,
  `Manifest coerente (identificatori dei test verificati: True)`.

Il primo controllo pubblico e il primo tentativo di leggere i 19 file della
baseline hanno restituito accesso negato nella sandbox. I confronti sono stati
ripetuti con accesso autorizzato e hanno prodotto gli esiti indicati sopra; il
primo tentativo non costituisce evidenza di file differenti. Un primo comando
`git rev-parse` con quoting PowerShell errato è fallito; la ripetizione con
argomento quotato ha restituito il tree indicato.

## Limiti e lavoro rimanente

Questa verifica è una fotografia degli input al momento del controllo, non una
garanzia per le run successive. Ripetere i controlli di integrità richiesti
prima di ogni run; per la CLI verificare anche dimensione e versione secondo P34.
Le configurazioni runtime sono generate per ciascuna run: applicare il contratto
P32 e verificare gli hash nei rispettivi record. Non sono state ripetute in
questa iterazione le prove di isolamento o le suite complete dell'evaluator;
le evidenze precedenti restano distinte da questi controlli.

Il prossimo passo è la formalizzazione del protocollo minimo dei sei nuovi
piloti. Questo documento non autorizza chiamate LLM, piloti o run ufficiali.
