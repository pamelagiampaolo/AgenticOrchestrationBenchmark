# A9 — Comando pubblico della gerarchia statica

## Iterazione

- **Data:** 9 novembre 2026.
- **Obiettivo:** rendere il runner A8 avviabile in modo riproducibile dalla riga
  di comando.
- **Componenti:** `RunStaticHierarchyCommand`, instradamento in `Program.cs` e
  test del parsing.
- **Accettazione:** input espliciti, tre deleghe non configurabili, errori
  classificati e nessuna chiamata LLM nei test.

## Contratto CLI

```text
run-static-hierarchy
  --prepared-run <assoluto>
  --claude <assoluto>
  --mcp-server <assoluto>
  --node <assoluto>
  --vitest <assoluto>
  --typescript <assoluto>
  --model <id>
  --time-limit-seconds <intero positivo>
  --token-budget <intero positivo>
```

Il comando non accetta `--max-delegations`: il piano A6 fissa sempre tre
passaggi. Tutti i percorsi della CLI, server e toolchain devono essere assoluti.

Exit code:

- `0`: run eseguita e record scritto, indipendentemente dallo stato sperimentale
  contenuto nel record;
- `1`: run non avviata o non registrata;
- `2`: argomenti non validi.

Lo stdout contiene lo stesso riepilogo JSON della gerarchia adattiva: run ID,
stato, token, durata, conteggi delle deleghe e percorso del record. La diagnostica
va su stderr.

## Verifiche osservate

- build: 0 avvisi, 0 errori;
- test dei comandi statico e adattivo: 17/17;
- richiesta valida: `MaxDelegations` costruito come 3;
- tempo, token e percorsi relativi non validi: rifiutati;
- `--max-delegations 4`: rifiutato come argomento sconosciuto;
- file di input inesistenti: exit code 1 senza output JSON.

## Prossimo passo

Preparare una nuova area T1 non ufficiale e avviare `run-static-hierarchy` con
Claude CLI reale. La prova deve controllare quattro processi, tre deleghe, uso
globale, audit MCP, integrità, UTF-8 e assenza di residui prima dei piloti B4.
