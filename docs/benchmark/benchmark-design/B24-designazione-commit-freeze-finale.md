# B24 — Designazione del commit di freeze finale

## Classificazione

- **Data:** 18 novembre 2026.
- **Fase:** freeze precedente ai nuovi piloti.
- **Stato:** commit sorgente definitivo designato; manifest degli artefatti,
  toolchain ed evaluator ancora da congelare.
- **Autorizzazione operativa:** nessuna chiamata LLM, pilota o run ufficiale è
  autorizzata da questo documento.

## Decisione

Il singolo commit sorgente designato come baseline e harness dei nuovi piloti e
delle successive run ufficiali è:

```text
aee8d1162bb496895990a00095cc14c2651ff752
```

Il relativo tree object Git è:

```text
7543e5dddc103dfbc7250566c26b765c370fbda0
```

Tutte le aree di lavoro sperimentali devono essere create esplicitamente da
questo commit. Il valore completo del commit costituisce anche il seed previsto
da B23 per generare il piano definitivo delle 54 run ufficiali.

## Evidenze considerate

Al momento della designazione:

- `git status --porcelain=v1` non ha restituito alcuna voce;
- P28 registra formattazione, build, suite .NET, typecheck, build TypeScript e
  suite pubbliche della baseline;
- P27 registra le prove MCP non-LLM;
- P29 registra la prova MCP attraverso Claude CLI e il modello esplicito
  `claude-sonnet-5-5`;
- rispetto al commit verificato in P27, le sole modifiche successive sono i log
  P28 e P29 e il pin esplicito del modello nello script del probe; non sono
  cambiate le soluzioni della baseline, l'orchestratore o il server MCP.

La designazione non anticipa né sostituisce il manifest degli hash richiesto da
B22. Gli hash devono essere calcolati sui file estratti da questo commit, non da
un futuro `HEAD` contenente documentazione amministrativa successiva al freeze.

## Regola per modifiche successive

I commit documentali creati per registrare manifest, toolchain, evaluator,
protocollo e risultati non cambiano il commit sorgente designato. Qualunque
modifica successiva al contenuto usato dall'harness o mostrato agli agenti deve
essere valutata secondo B22. Se può influire su comportamento, durata, token o
valutazione, questa designazione viene invalidata e occorrono un nuovo commit di
freeze e nuovi piloti.

## Lavoro rimanente nel freeze

Prima dei nuovi piloti restano da:

1. registrare gli hash di benchmark, specifiche T1–T6, istruzioni, eseguibili,
   configurazioni MCP e materiali pubblici estratti dal commit designato;
2. creare e verificare la toolchain definitiva dal lockfile congelato;
3. congelare evaluator, test riservati e schema dei record senza esporre
   materiali riservati agli agenti;
4. definire e approvare il protocollo minimo dei sei nuovi piloti.
