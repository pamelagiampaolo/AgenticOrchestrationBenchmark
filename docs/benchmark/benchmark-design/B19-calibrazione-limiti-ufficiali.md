# B19 — Calibrazione storica dei limiti

> **Stato superato per le future run ufficiali.** I valori di questo documento
> restano un'evidenza riproducibile della calibrazione eseguita sul commit
> `2d234ec410c89e7fb4999534e4f57ad8e4a4c2af`, ma non sono applicabili a un
> successivo commit di readiness con istruzioni o toolchain differenti. La
> politica corrente di freeze e ricalibrazione è definita in
> [B22](B22-politica-freeze-e-ricalibrazione.md).

## Classificazione

- **Data:** 10 novembre 2026.
- **Stato:** calibrazione completata per la configurazione B19; sostituita da B22
  per le future run ufficiali.
- **Protocollo applicato:** B6, approvato prima dei piloti.
- **Piloti considerati:** esclusivamente i sei run ID della allowlist B18.
- **Commit sorgente e harness comune:**
  `2d234ec410c89e7fb4999534e4f57ad8e4a4c2af`.

## Controlli preliminari

I valori sono stati letti direttamente dai `run-record.json` o
`session-record.json` conservati in `C:\AgenticAIResults`, non trascritti dai
documenti riepilogativi. Per ciascun pilota sono stati verificati:

- run ID, task e architettura attesi;
- stato `completed`;
- commit sorgente comune;
- misurazione token completa;
- outcome `passed` del Trusted Evaluator.

## Dati aggregati

| Ordine | Run ID | Task | Architettura | Durata agente (ms) | Token totali |
| ---: | --- | --- | --- | ---: | ---: |
| 1 | `20261110T161912Z-359958f0` | T1 | adattiva | 24.974 | 38.216 |
| 2 | `20261110T162200Z-914660f7` | T1 | statica | 92.817 | 190.293 |
| 3 | `20261110T162624Z-2e6886a2` | T1 | single-agent | 25.614 | 36.916 |
| 4 | `20261110T162926Z-e66b80f6` | T6 | statica | 103.647 | 186.811 |
| 5 | `20261110T163437Z-dc161db1` | T6 | single-agent | 31.941 | 64.072 |
| 6 | `20261110T164117Z-f4ac9e29` | T6 | adattiva | 31.739 | 58.219 |

Il massimo temporale e il massimo dei token provengono da due piloti diversi.
Come prescritto da B6, ciascuna metrica usa il proprio massimo congiunto sulle
tre architetture e sui due task; non vengono usate medie né limiti separati.

## Calcolo riproducibile

### Limite di tempo

```text
max(agentTaskDurationMs) = 103.647 ms
103.647 × 1,25 = 129.558,75 ms = 129,55875 secondi
arrotondamento per eccesso a multipli di 30 secondi = 150 secondi
```

Il massimo osservato è il pilota T6 con gerarchia statica
`20261110T162926Z-e66b80f6`.

### Budget token

```text
max(totalTokens) = 190.293
190.293 × 1,25 = 237.866,25 token
arrotondamento per eccesso a multipli di 50.000 token = 250.000 token
```

Il massimo osservato è il pilota T1 con gerarchia statica
`20261110T162200Z-914660f7`.

## Limiti congelati per la configurazione B19

I valori seguenti erano stati congelati per run basate sulla stessa configurazione
dei sei piloti B13–B18:

| Parametro | Valore ufficiale |
| --- | ---: |
| Limite di tempo | **150 secondi** |
| Budget globale | **250.000 totalTokens** |
| Periodo tecnico aggiuntivo | **0 secondi** |
| Massimo deleghe adattive | **3** |

Parametri CLI corrispondenti:

```text
--time-limit-seconds 150 --token-budget 250000
```

Per la sola gerarchia adattiva resta inoltre obbligatorio:

```text
--max-delegations 3
```

La gerarchia statica conserva esattamente le tre deleghe previste dalla propria
architettura. Il limite di 150 secondi riguarda `agentTaskDurationMs` e il budget
di 250.000 riguarda il totale globale autorevole di tutti gli agenti della run.

## Conseguenze storiche del freeze

- i tetti pilota di 900 secondi e 1.000.000 token non erano valori per le run
  ufficiali;
- non si ricalibrano i limiti usando risultati delle run ufficiali;
- non si introducono limiti differenti per task o architettura;
- qualunque modifica ai limiti, al margine o agli arrotondamenti richiede una
  nuova decisione esplicita e una nuova calibrazione precedente alle run
  ufficiali;
- questa iterazione non avvia alcuna run ufficiale e non modifica il codice
  dell'harness, perché i parametri sono input obbligatori della CLI.

La configurazione futura non deve riutilizzare automaticamente questi valori. Il
passo successivo è completare la readiness, congelare la configurazione finale e
applicare la ricalibrazione prescritta da B22.
