# A10 — Prova reale della gerarchia statica

## Iterazione

- **Data:** 9 novembre 2026.
- **Obiettivo:** verificare con Claude CLI reale il percorso completo
  `coordinator → analyst → implementer → verifier`.
- **Baseline:** commit `d9dc66a480dcb48ca61a8dc7cf02e482020659ba`,
  task T1, run `20261109T153037Z-d1a6053d`.
- **Limiti:** 600 secondi, 1.000.000 token, modello `claude-sonnet-5-5`.
- **Classificazione:** prova tecnica non ufficiale, non pilota B4.

## Esito complessivo

La run è terminata con exit code 0 e stato `completed` in 111.024 ms.
Sono state richieste, avviate e completate esattamente tre deleghe; nessuna è
fallita o stata rifiutata. Il massimo osservato di lavoratori contemporanei è 1.

Il record contiene, nell'ordine:

1. `coordinator`, ruolo `coordinator`;
2. `analyst`, ruolo `analyst`;
3. `implementer`, ruolo `implementer`;
4. `verifier`, ruolo `verifier`.

Tutti i processi hanno exit code 0, risultato `success`, uso autorevole completo,
zero violazioni di configurazione e zero errori strumento.

## Uso globale

Il totale autorevole è 199.097 token:

| Ruolo | Token totali |
| --- | ---: |
| coordinator | 37.719 |
| analyst | 52.981 |
| implementer | 31.397 |
| verifier | 77.000 |

Componenti complessive: 40 input non in cache, 140.596 cache read, 45.500 cache
creation e 12.961 output. La somma delle componenti e dei ruoli coincide con il
totale registrato.

## Coordinamento osservato

Il coordinatore ha chiamato soltanto `delegate_task`, tre volte. L'analyst ha
usato strumenti di lettura e non ha modificato file. L'implementer ha scritto
`workspace/src/index.ts` ed eseguito typecheck, build e test pubblici. Il verifier
ha ripetuto le tre verifiche, creato quattro test aggiuntivi e chiamato
`run_agent_tests`.

L'implementer ha ricevuto un piano sintetizzato dell'analyst. Il verifier ha
ricevuto i riepiloghi sintetizzati di analyst e implementer. Il trasferimento è
osservabile nei rispettivi `prompt.md` persistiti.

## Verifiche di sviluppo

- typecheck: completato due volte, exit 0;
- build: completata due volte, exit 0;
- suite pubblica: completata due volte, 31/31 secondo i riepiloghi;
- test agente: completati, quattro test aggiuntivi superati;
- record audit: sette operazioni, tutte `completed`, exit 0.

## Integrità e pulizia

- differenze nei materiali protetti: zero;
- UTF-8: `quantità` corretto, nessun mojibake o carattere sostitutivo;
- residui worker: zero;
- file nella session directory: zero;
- processi MCP residui: zero;
- relay di coordinamento residui: zero;
- stderr dei quattro agenti: vuoto.

Le evidenze sono in
`C:\AgenticAIResults\20261109T153037Z-d1a6053d`; l'area modificata è in
`C:\AgenticAIRuns\20261109T153037Z-d1a6053d`.

## Conclusione

La gerarchia statica è operativa nel percorso reale e applica il piano approvato.
Questa prova non fissa limiti né risultati sperimentali. Prima dei piloti restano
da congelare workload di calibrazione, numero di prove, regola del margine,
ordine/seed e istruzioni definitive comuni alle tre architetture.
