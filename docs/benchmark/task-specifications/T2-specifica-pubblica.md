# T2 — Carico manuale di magazzino

## Registro

- **Data:** 4 novembre 2026
- **Stato:** contratto e pacchetto di test pubblici approvati
- **Perimetro:** specifica pubblica e quattro casi T2; nessuna soluzione inclusa

## Contratto pubblico

`InventoryManagement` deve esporre questa operazione pubblica:

```ts
receiveStock(productId: string, quantity: number): number;
```

L'operazione incrementa la scorta del prodotto indicato e restituisce la nuova
quantità disponibile. Carichi successivi sullo stesso prodotto si sommano.

`quantity` deve essere un intero sicuro strettamente positivo, verificato tramite
`Number.isSafeInteger`. Anche la scorta ottenuta dalla somma deve essere un intero
sicuro. Un prodotto inesistente produce un errore con proprietà stabile `code`
uguale a `PRODUCT_NOT_FOUND`. Una quantità ricevuta non valida o una somma non
sicura produce un errore con `code` uguale a `INVALID_QUANTITY`. Gli errori
seguono la struttura della baseline; il messaggio testuale non è vincolante.

Un carico valido modifica soltanto la scorta del prodotto indicato. Ogni errore
lascia invariati catalogo, scorte e ordini. Stato e dati restano indipendenti tra
istanze diverse di `InventoryManagement`.

## Fixture pubblica

| Prodotto | Nome | Quantità iniziale |
| --- | --- | ---: |
| `P-001` | Tastiera | 0 |
| `P-002` | Mouse | 5 |
| `P-003` | Monitor | 10 |

Ogni caso costruisce una nuova istanza dalla fixture.

## Casi pubblici

| ID | Operazione | Risultato atteso |
| --- | --- | --- |
| `T2-P01` | carica `3` su `P-001` | restituisce `3`; scorta `3`; resto dello stato invariato |
| `T2-P02` | carica `2`, poi `4`, su `P-002` | restituisce `7`, poi `11`; scorte corrispondenti; resto dello stato invariato |
| `T2-P03` | carica `0` su `P-003` | errore `INVALID_QUANTITY`; intero stato osservabile invariato |
| `T2-P04` | carica `1` su `P-999` | errore `PRODUCT_NOT_FOUND`; intero stato osservabile invariato |

Il task è completato soltanto se continuano a passare anche tutti i test pubblici
comuni della baseline. I test riservati non fanno parte di questo pacchetto.
