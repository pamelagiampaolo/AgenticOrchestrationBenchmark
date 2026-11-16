# B7 — Pilota 1: T1 con gerarchia adattiva

## Classificazione

- **Data:** 9 novembre 2026.
- **Stato:** pilota valido per la calibrazione B6.
- **Ordine:** primo dei sei piloti congelati.
- **Run ID:** `20261109T195256Z-9fc50de5`.
- **Task:** T1.
- **Architettura:** `hierarchical-adaptive`.
- **Commit sorgente:** `748e09f6b3d315027e554dd2af7035ad0fc78a96`.

Il pilota non appartiene alle 54 run ufficiali e non viene usato per confrontare
la qualità delle architetture. Il suo tempo e i suoi token saranno usati soltanto
insieme agli altri cinque piloti per applicare la regola di calibrazione B6.

## Parametri congelati

- modello: `claude-sonnet-5-5`;
- limite di tempo: 900 secondi;
- budget globale: 1.000.000 token;
- massimo deleghe: 3;
- periodo tecnico di arresto: 0 secondi;
- CLI Claude: `2.1.312 (Claude Code)`.

## Preflight

Il repository principale era pulito sul commit sorgente. La suite dell'harness è
stata eseguita con:

```powershell
dotnet test AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --configuration Debug --no-restore
```

Esito osservato: 299 test totali, 298 superati, 0 falliti e 1 probe reale
esplicitamente esclusa; durata 22,810 secondi. Erano disponibili Claude CLI
2.1.312, Node v24.17.0 e .NET SDK 10.0.403.

## Preparazione ed esecuzione

`prepare-run` ha creato una nuova area da T1 e dal commit sorgente, con stdout
JSON valido e senza riuso di workspace o sessioni. La gerarchia adattiva è stata
poi avviata con i parametri congelati B6.

Esito osservato del runner:

| Campo | Valore |
| --- | ---: |
| stato | `completed` |
| `agentTaskDurationMs` | 35.147 |
| `totalTokens` | 38.261 |
| input non in cache | 8 |
| cache read | 20.355 |
| cache creation | 13.418 |
| output | 4.480 |
| deleghe richieste/avviate/completate | 0/0/0 |
| massimo lavoratori concorrenti | 0 |

Il coordinatore ha scelto di non delegare. Questo è un comportamento ammesso
dalla condizione adattiva e non rende invalida la prova. L'uso autorevole è
completo e coincide con quello ricostruito dallo stream.

## Verifiche dell'harness

- modello richiesto e osservato coincidenti;
- server MCP workspace e coordinamento connessi;
- strumenti esposti conformi al profilo adattivo;
- violazioni di configurazione: 0;
- errori strumento: 0;
- righe stream malformate: 0;
- stderr del coordinatore vuoto;
- typecheck eseguito, exit code 0;
- suite pubblica eseguita, exit code 0;
- differenze nei materiali protetti: 0;
- file UTF-8 non validi: 0;
- directory di sessione vuota al termine;
- processi residui dell'orchestratore o del server MCP: 0.

L'audit MCP contiene due operazioni completate: typecheck e test pubblici.

## Valutazione indipendente

Il valutatore fidato `<trusted-evaluator-root>`, commit
`eb573d5a53985abe0bc809299c64a35f300443fd`, ha valutato la submission congelata
con toolchain Node v24.17.0, TypeScript 7.0.2 e Vitest 5.1.0.

Esito normalizzato:

- outcome `passed`, exit code 0;
- build superata;
- test pubblici: 31 attesi, 31 eseguiti, 31 superati;
- test riservati: 3 attesi, 3 eseguiti, 3 superati;
- test mancanti, inattesi o saltati: 0;
- input originale invariato;
- submission archiviata invariata;
- manifest delle suite verificati;
- diagnostica dell'evaluator vuota;
- durata della valutazione: 4.862 ms.

La baseline canonica dichiarata dall'evaluator è
`ddf08e8759ef172a5b654168a1d2d6b66c9cd248`. Il comando seguente non ha prodotto
differenze per i materiali applicabili:

```powershell
git diff --name-status ddf08e8759ef172a5b654168a1d2d6b66c9cd248 748e09f6b3d315027e554dd2af7035ad0fc78a96 -- BenchmarkTarget docs/benchmark/T1-specifica-pubblica.md
```

## Evidenze e limite della conclusione

Le evidenze dell'harness sono conservate in
`C:\AgenticAIResults\20261109T195256Z-9fc50de5` e la workspace congelata in
`C:\AgenticAIRuns\20261109T195256Z-9fc50de5`. Le evidenze private della
valutazione sono in
`<trusted-evaluator-root>\runs\20261109T195256Z-9fc50de5`.

Questa prova certifica la validità del solo slot T1 adattivo. Non certifica gli
altri cinque piloti, non determina ancora i limiti ufficiali e non dimostra che
la gerarchia adattiva deleghi su workload differenti.
