import { beforeEach, describe, expect, test } from "vitest";

import {
  InventoryManagement,
  type InitialProduct,
} from "../../../src/index.js";

describe("T6 - partial fulfillment", () => {
  let inventory: InventoryManagement;

  beforeEach(() => {
    const initialProducts: InitialProduct[] = [
      { productId: "P-001", name: "Tastiera", availableQuantity: 0 },
      { productId: "P-002", name: "Mouse", availableQuantity: 5 },
      { productId: "P-003", name: "Monitor", availableQuantity: 10 },
    ];
    inventory = new InventoryManagement(initialProducts);
  });

  test("T6-P01 confirms an order as waiting when no stock is available", () => {
    const stateBefore = readObservableState(inventory);
    const order = inventory.createOrder([
      { productId: "P-001", quantity: 4 },
    ]);
    expect(order).toEqual(expectedOrder("draft", 0, 4));

    const confirmed = inventory.confirmOrder(order.orderId);

    expect(confirmed).toEqual(expectedOrder("waiting", 0, 4));
    expect(readObservableState(inventory)).toEqual(
      stateWithOrder(stateBefore, confirmed),
    );
    assertDetachedOrderSnapshot(inventory, confirmed);
  });

  test("T6-P02 partially fulfills an order after receiving stock", () => {
    const stateBefore = readObservableState(inventory);
    const order = createAndConfirmWaitingOrder(inventory);

    expect(inventory.receiveStock("P-001", 2)).toBe(2);
    const fulfilled = inventory.fulfillOrder(order.orderId);

    expect(fulfilled).toEqual(expectedOrder("partially-fulfilled", 2, 2));
    expect(readObservableState(inventory)).toEqual(
      stateWithOrder(stateBefore, fulfilled),
    );
  });

  test("T6-P03 completes an order through successive fulfillment attempts", () => {
    const stateBefore = readObservableState(inventory);
    const order = createAndConfirmWaitingOrder(inventory);
    inventory.receiveStock("P-001", 2);
    expect(inventory.fulfillOrder(order.orderId)).toEqual(
      expectedOrder("partially-fulfilled", 2, 2),
    );

    expect(inventory.receiveStock("P-001", 2)).toBe(2);
    const fulfilled = inventory.fulfillOrder(order.orderId);

    expect(fulfilled).toEqual(expectedOrder("fulfilled", 4, 0));
    expect(readObservableState(inventory)).toEqual(
      stateWithOrder(stateBefore, fulfilled),
    );
  });

  test("T6-P04 rejects fulfillment of a completed order without mutations", () => {
    const order = createAndConfirmWaitingOrder(inventory);
    inventory.receiveStock("P-001", 2);
    inventory.fulfillOrder(order.orderId);
    inventory.receiveStock("P-001", 2);
    inventory.fulfillOrder(order.orderId);
    const stateBeforeExtraFulfillment = readObservableState(inventory);

    expect(() => inventory.fulfillOrder(order.orderId)).toThrowError(
      expect.objectContaining({ code: "ORDER_ALREADY_FULFILLED" }),
    );
    expect(readObservableState(inventory)).toEqual(
      stateBeforeExtraFulfillment,
    );
  });

  test("T6-P05 fully fulfills an order when enough stock is available", () => {
    const stateBefore = readObservableState(inventory);
    const created = inventory.createOrder([
      { productId: "P-002", quantity: 3 },
    ]);
    expect(created).toEqual({
      orderId: "O-001",
      status: "draft",
      lines: [
        {
          productId: "P-002",
          requestedQuantity: 3,
          fulfilledQuantity: 0,
          remainingQuantity: 3,
        },
      ],
    });

    const confirmed = inventory.confirmOrder(created.orderId);

    expect(confirmed).toEqual({
      orderId: "O-001",
      status: "fulfilled",
      lines: [
        {
          productId: "P-002",
          requestedQuantity: 3,
          fulfilledQuantity: 3,
          remainingQuantity: 0,
        },
      ],
    });
    expect(readObservableState(inventory)).toEqual(
      stateWithOrder(stateBefore, confirmed, { "P-002": 2 }),
    );
  });
});

function createAndConfirmWaitingOrder(inventory: InventoryManagement) {
  const order = inventory.createOrder([{ productId: "P-001", quantity: 4 }]);
  expect(order).toEqual(expectedOrder("draft", 0, 4));
  expect(inventory.confirmOrder(order.orderId)).toEqual(
    expectedOrder("waiting", 0, 4),
  );
  return order;
}

function expectedOrder(
  status: "draft" | "waiting" | "partially-fulfilled" | "fulfilled",
  fulfilledQuantity: number,
  remainingQuantity: number,
) {
  return {
    orderId: "O-001",
    status,
    lines: [
      {
        productId: "P-001",
        requestedQuantity: 4,
        fulfilledQuantity,
        remainingQuantity,
      },
    ],
  };
}

function readObservableState(inventory: InventoryManagement) {
  const products = inventory.listProducts();

  return {
    products,
    availableQuantities: products.map(({ productId }) => ({
      productId,
      availableQuantity: inventory.getAvailableQuantity(productId),
    })),
    orders: inventory.listOrders(),
  };
}

function stateWithOrder(
  state: ReturnType<typeof readObservableState>,
  order: ReturnType<typeof expectedOrder>,
  changedQuantities: Readonly<Record<string, number>> = {},
): ReturnType<typeof readObservableState> {
  return {
    ...state,
    availableQuantities: state.availableQuantities.map((stock) => ({
      ...stock,
      availableQuantity:
        changedQuantities[stock.productId] ?? stock.availableQuantity,
    })),
    orders: [order],
  };
}

function assertDetachedOrderSnapshot(
  inventory: InventoryManagement,
  order: ReturnType<typeof expectedOrder>,
): void {
  order.lines[0]!.fulfilledQuantity = 99;
  expect(inventory.getOrder(order.orderId)).toEqual(
    expectedOrder("waiting", 0, 4),
  );
}
