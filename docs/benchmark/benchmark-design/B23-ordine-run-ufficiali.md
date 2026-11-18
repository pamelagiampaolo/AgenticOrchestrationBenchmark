# B23 — Ordine riproducibile delle run ufficiali

## Classificazione

- **Data:** 18 novembre 2026.
- **Fase:** readiness precedente al freeze finale.
- **Stato:** algoritmo e validatore implementati; piano definitivo non ancora
  generato perché il commit finale non esiste.
- **Autorizzazione operativa:** nessuna run o chiamata LLM autorizzata da questo
  documento.

## Decisione

Le 54 run ufficiali sono organizzate in 18 blocchi, uno per ogni coppia tra i
task T1–T6 e le ripetizioni 1–3. Ogni blocco contiene esattamente una run per
ciascuna architettura:

- `single-agent`;
- `hierarchical-static`;
- `hierarchical-adaptive`.

Il seed è l'object ID Git completo, in esadecimale minuscolo, del commit finale
di freeze. Il formato è quello del repository (`40` caratteri per SHA-1 oppure
`64` per SHA-256). Non viene scelto o modificato dopo aver osservato risultati.

## Algoritmo congelabile

L'identificatore dell'algoritmo è `sha256-blocked-v1`.

1. Per ogni coppia task–ripetizione si calcola SHA-256 della stringa UTF-8
   `<seed>|block|<task>|<repetition>`.
2. I 18 blocchi sono ordinati lessicograficamente per hash; task e ripetizione
   risolvono deterministicamente un'eventuale collisione.
3. Dentro ogni blocco, per ciascuna architettura si calcola SHA-256 della stringa
   UTF-8 `<seed>|<task>|<repetition>|<architecture>`.
4. Le tre architetture sono ordinate lessicograficamente per hash; il nome
   dell'architettura risolve deterministicamente un'eventuale collisione.
5. Le posizioni globali sono assegnate consecutivamente da 1 a 54.

L'ordine a blocchi limita sequenze sbilanciate senza rendere l'ordine dipendente
dai risultati sperimentali.

## Artefatto e comando

Il comando seguente scrive il piano JSON su stdout e non crea file:

```powershell
dotnet run --project .\src\AgenticExperimentRunner\AgenticExperimentRunner.csproj -- `
  generate-experiment-plan --seed <object-id-git-completo>
```

Il piano contiene `schemaVersion`, seed, algoritmo e 54 slot. Ogni slot registra
posizione globale, blocco, identificatore, task, architettura, ripetizione e
`runKind=official`.

Poiché il seed è l'hash del commit finale, il JSON definitivo non viene inserito
nello stesso commit: ciò creerebbe una dipendenza circolare e cambierebbe il
seed. Dopo il freeze, il controllore fidato deve:

1. generare il JSON fuori dal workspace accessibile agli agenti;
2. validarlo con la stessa versione dell'orchestratore;
3. registrarne percorso fidato e SHA-256 nelle evidenze di freeze;
4. usarlo come unica fonte di `repetition`, `orderSeed` e `orderPosition`;
5. conservarlo immutato insieme ai risultati.

## Sostituzioni

Una run sostitutiva non occupa una nuova posizione e non modifica il piano. Deve
conservare task, architettura, ripetizione, seed e posizione dello slot originale,
ricevere un nuovo `runId` e registrare separatamente il collegamento alla run
invalidata. La politica che autorizza una sostituzione resta quella congelata
prima dell'esperimento.

## Criteri di accettazione automatizzati

- seed uguale a un object ID Git completo di 40 o 64 caratteri esadecimali
  minuscoli;
- 54 slot e posizioni consecutive 1–54;
- 18 blocchi, ciascuno con le tre architetture una sola volta;
- copertura esatta di 6 task × 3 ripetizioni × 3 architetture;
- identificatori di slot univoci;
- tutte le run classificate `official`;
- JSON byte-identico a parità di seed e versione.

Il piano definitivo resta da generare e verificare dopo la designazione del
commit finale prevista da B22.
