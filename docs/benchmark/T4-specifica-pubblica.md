# T4 — Sospensione di un prodotto dal catalogo

## Registro

- **Data:** 4 novembre 2026
- **Stato:** contratto e pacchetto di test pubblici approvati
- **Perimetro:** specifica pubblica e quattro casi T4; nessuna soluzione inclusa

## Contratto pubblico

Il tipo `Product` e `listProducts()` restano invariati. L'entry point
`src/index.ts` deve esportare:

```ts
type ProductWithStatus = {
  productId: string;
  name: string;
  status: "active" | "suspended";
};
```

`InventoryManagement` deve esporre:

```ts
listProductsWithStatus(): ProductWithStatus[];
suspendProduct(productId: string): ProductWithStatus;
reactivateProduct(productId: string): ProductWithStatus;
```

Tutti i prodotti iniziano `active`, senza aggiungere lo stato all'input iniziale.
La query restituisce copie indipendenti, ordinate per `productId` testuale
crescente, e non modifica lo stato. Le transizioni consentite sono
`active → suspended` e `suspended → active`; entrambe restituiscono uno snapshot
indipendente del prodotto aggiornato e non modificano scorte o ordini.

Gli errori espongono, secondo la struttura della baseline, uno dei codici stabili
`PRODUCT_NOT_FOUND`, `PRODUCT_ALREADY_SUSPENDED`, `PRODUCT_ALREADY_ACTIVE` e
`PRODUCT_SUSPENDED`. Il messaggio testuale non è vincolante e ogni errore è
atomico.

La creazione di un ordine `draft` con prodotti sospesi è consentita. La conferma
di un ordine contenente almeno un prodotto sospeso produce `PRODUCT_SUSPENDED`,
senza modificare l'ordine, le scorte, il catalogo o gli stati dei prodotti. Gli
ordini già confermati restano invariati e consultabili. Dopo la riattivazione, un
ordine che contiene il prodotto può essere confermato normalmente.

T4 parte dalla baseline originale e non comprende le soluzioni T1–T3.

## Fixture pubblica

| Prodotto | Nome | Quantità iniziale |
| --- | --- | ---: |
| `P-001` | Tastiera | 0 |
| `P-002` | Mouse | 5 |
| `P-003` | Monitor | 10 |

Ogni caso costruisce una nuova istanza dalla fixture.

## Casi pubblici

| ID | Sequenza | Risultato atteso |
| --- | --- | --- |
| `T4-P01` | sospendi `P-002` | snapshot completo `suspended`; query coerente; scorta `5`; resto invariato |
| `T4-P02` | sospendi `P-002`, crea ordine `P-002 × 1`, tenta la conferma | `PRODUCT_SUSPENDED`; stato precedente alla conferma invariato; ordine `draft` |
| `T4-P03` | sospendi e riattiva `P-002`, crea e conferma ordine `P-002 × 1` | snapshot completi; prodotto `active`; ordine completo `confirmed`; scorta `4`; resto invariato |
| `T4-P04` | sospendi due volte `P-003` | secondo tentativo in errore `PRODUCT_ALREADY_SUSPENDED`; stato precedente invariato |

Il task è completato soltanto se continuano a passare anche tutti i test pubblici
comuni della baseline. I test riservati non fanno parte di questo pacchetto.
