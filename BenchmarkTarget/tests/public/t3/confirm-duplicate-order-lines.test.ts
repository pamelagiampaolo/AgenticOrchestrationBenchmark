import { beforeEach, describe, expect, test } from "vitest";

import {
  InventoryManagement,
  type InitialProduct,
  type OrderLine,
} from "../../../src/index.js";

describe("T3 - confirmation of orders with duplicate lines", () => {
  let inventory: InventoryManagement;

  beforeEach(() => {
    const initialProducts: InitialProduct[] = [
      { productId: "P-001", name: "Tastiera", availableQuantity: 0 },
      { productId: "P-002", name: "Mouse", availableQuantity: 5 },
      { productId: "P-003", name: "Monitor", availableQuantity: 10 },
    ];
    inventory = new InventoryManagement(initialProducts);
  });

  test("T3-P01 confirms duplicate lines at the exact stock limit", () => {
    const lines: OrderLine[] = [
      { productId: "P-002", quantity: 2 },
      { productId: "P-002", quantity: 3 },
    ];
    const order = inventory.createOrder(lines);
    const stateBeforeConfirmation = readObservableState(inventory);

    const confirmed = inventory.confirmOrder(order.orderId);

    expect(confirmed).toEqual({
      orderId: "O-001",
      status: "confirmed",
      lines,
    });
    expect(inventory.getAvailableQuantity("P-002")).toBe(0);
    expect(readObservableState(inventory)).toEqual(
      afterConfirmation(stateBeforeConfirmation, order.orderId, {
        "P-002": 0,
      }),
    );

    confirmed.lines[0]!.quantity = 99;
    expect(inventory.getOrder(order.orderId).lines).toEqual(lines);
  });

  test("T3-P02 rejects duplicate lines exceeding available stock atomically", () => {
    const order = inventory.createOrder([
      { productId: "P-002", quantity: 3 },
      { productId: "P-002", quantity: 3 },
    ]);
    const stateBeforeConfirmation = readObservableState(inventory);

    expect(() => inventory.confirmOrder(order.orderId)).toThrowError(
      expect.objectContaining({ code: "INSUFFICIENT_STOCK" }),
    );
    expect(readObservableState(inventory)).toEqual(stateBeforeConfirmation);
    expect(inventory.getOrder(order.orderId).status).toBe("draft");
  });

  test("T3-P03 preserves confirmation behavior without duplicate lines", () => {
    const lines: OrderLine[] = [
      { productId: "P-002", quantity: 2 },
      { productId: "P-003", quantity: 4 },
    ];
    const order = inventory.createOrder(lines);
    const stateBeforeConfirmation = readObservableState(inventory);

    const confirmed = inventory.confirmOrder(order.orderId);

    expect(confirmed).toEqual({
      orderId: "O-001",
      status: "confirmed",
      lines,
    });
    expect(inventory.getAvailableQuantity("P-002")).toBe(3);
    expect(inventory.getAvailableQuantity("P-003")).toBe(6);
    expect(readObservableState(inventory)).toEqual(
      afterConfirmation(stateBeforeConfirmation, order.orderId, {
        "P-002": 3,
        "P-003": 6,
      }),
    );

    confirmed.lines[0]!.quantity = 99;
    expect(inventory.getOrder(order.orderId).lines).toEqual(lines);
  });
});

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

function afterConfirmation(
  state: ReturnType<typeof readObservableState>,
  orderId: string,
  availableQuantities: Readonly<Record<string, number>>,
): ReturnType<typeof readObservableState> {
  return {
    ...state,
    availableQuantities: state.availableQuantities.map((stock) => ({
      ...stock,
      availableQuantity:
        availableQuantities[stock.productId] ?? stock.availableQuantity,
    })),
    orders: state.orders.map((order) =>
      order.orderId === orderId ? { ...order, status: "confirmed" } : order,
    ),
  };
}
