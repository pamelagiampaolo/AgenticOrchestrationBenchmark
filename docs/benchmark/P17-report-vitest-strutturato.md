# P17 - Report Vitest strutturato e diagnostica ripulita

## Data e obiettivo

- Data: 2026-11-08.
- Obiettivo: acquisire il report pubblico prima della cancellazione della
  composizione P16 e restituire conteggi verificabili senza percorsi assoluti
  noti.

## Comportamento

La configurazione generata ordina al reporter JSON di scrivere
`output/vitest-results.json`, fuori dalla directory `project/` ma dentro
l'invocazione effimera. Prima della cancellazione il worker valida come interi
non negativi:

- test totali, superati, falliti e pending;
- suite totali, superate, fallite e pending.

La somma degli esiti dei test deve coincidere con il totale. Report assente,
JSON invalido, proprietà mancanti o conteggi incoerenti producono lo stato
`infrastructure-error`; non vengono confusi con test falliti.

Il risultato MCP aggiunge `Tests` con i soli conteggi. Dalla diagnostica vengono
sostituiti i percorsi conosciuti dell'invocazione, della root agente, della
toolchain Vitest e di Node. Il report completo viene eliminato insieme alla
composizione e non viene esposto all'agente.

## Verifiche osservate

- suite Vitest pubblica comune reale: 27 totali, 27 superati, exit code 0;
- report mancante: classificato `infrastructure-error` e conteggi assenti;
- percorso della worker root: assente dalla diagnostica restituita;
- test mirati P15-P17: 5/5;
- formattazione: exit code 0;
- build: 0 avvisi, 0 errori;
- suite .NET completa: 71/71;
- `git diff --check`: nessun errore.

## Limiti

La rimozione riguarda i percorsi assoluti configurati e osservabili dal
controllore; non è una garanzia generale contro testo arbitrario stampato dal
codice della submission. Il processo resta privo di isolamento preventivo come
deciso in P15.

Il prossimo passo operativo è aggiornare il launcher della sessione affinché
imposti le variabili P16 per la run preparata e abiliti esplicitamente
`run_public_tests` nella allowlist degli strumenti dell'agente.
