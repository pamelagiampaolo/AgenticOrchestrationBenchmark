# T1 — Ricerca dei prodotti sotto scorta

## Registro

- **Data:** 4 novembre 2026
- **Stato:** contratto e pacchetto di test pubblici approvati
- **Perimetro:** specifica pubblica e quattro casi T1; nessuna soluzione inclusa

## Contratto pubblico

La submission deve esportare dall'entry point `src/index.ts` il tipo seguente:

```ts
type ProductStockSnapshot = {
  productId: string;
  name: string;
  availableQuantity: number;
};
```

`InventoryManagement` deve esporre questa operazione pubblica:

```ts
findProductsBelowStock(threshold: number): ProductStockSnapshot[];
```

La soglia deve essere un numero intero maggiore o uguale a zero. Una soglia
negativa, decimale o non numerica deve produrre un errore con proprietà stabile
`code` uguale a `INVALID_THRESHOLD`; il messaggio testuale non è vincolante.

L'operazione include soltanto i prodotti la cui quantità disponibile è
strettamente minore della soglia. Restituisce snapshot contenenti esclusivamente
`productId`, `name` e `availableQuantity`, ordinati per `productId` testuale
crescente. Gli snapshot sono copie indipendenti: la loro modifica non altera lo
stato interno. La query non modifica catalogo, scorte o ordini.

## Fixture pubblica

| Prodotto | Nome | Quantità iniziale |
| --- | --- | ---: |
| `P-001` | Tastiera | 0 |
| `P-002` | Mouse | 5 |
| `P-003` | Monitor | 10 |

## Casi pubblici

| ID | Input | Risultato atteso |
| --- | --- | --- |
| `T1-P01` | soglia `6` | `P-001`/Tastiera/`0` e `P-002`/Mouse/`5`, in quest'ordine |
| `T1-P02` | soglia `0` | collezione vuota |
| `T1-P03` | soglia `-1` | errore `INVALID_THRESHOLD`; stato invariato |
| `T1-P04` | soglia `2.5` | errore `INVALID_THRESHOLD`; stato invariato |

Il task è completato soltanto se continuano a passare anche tutti i test pubblici
comuni della baseline. I test riservati non fanno parte di questo pacchetto.
