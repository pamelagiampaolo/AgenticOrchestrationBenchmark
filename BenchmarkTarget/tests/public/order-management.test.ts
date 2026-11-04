import { beforeEach, describe, expect, test } from "vitest";

import {
  InventoryManagement,
  type InitialProduct,
  type OrderLine,
} from "../../src/index.js";

describe("order management", () => {
  let initialProducts: InitialProduct[];
  let inventory: InventoryManagement;

  beforeEach(() => {
    initialProducts = [
      { productId: "P-001", name: "Tastiera", availableQuantity: 0 },
      { productId: "P-002", name: "Mouse", availableQuantity: 5 },
      { productId: "P-003", name: "Monitor", availableQuantity: 10 },
    ];
    inventory = new InventoryManagement(initialProducts);
  });

  test("creates and reads a draft order without changing stock", () => {
    const created = inventory.createOrder([
      { productId: "P-002", quantity: 2 },
    ]);

    expect(created).toEqual({
      orderId: "O-001",
      status: "draft",
      lines: [{ productId: "P-002", quantity: 2 }],
    });
    expect(inventory.getOrder("O-001")).toEqual(created);
    expect(inventory.getAvailableQuantity("P-002")).toBe(5);
  });

  test("lists orders by textual identifier", () => {
    inventory.createOrder([{ productId: "P-002", quantity: 1 }]);
    inventory.createOrder([{ productId: "P-003", quantity: 1 }]);
    inventory.createOrder([{ productId: "P-002", quantity: 2 }]);

    expect(inventory.listOrders().map(({ orderId }) => orderId)).toEqual([
      "O-001",
      "O-002",
      "O-003",
    ]);
  });

  test("does not consume an identifier when creation fails", () => {
    expectErrorCode(() => inventory.createOrder([]), "EMPTY_ORDER");

    expect(
      inventory.createOrder([{ productId: "P-002", quantity: 1 }]).orderId,
    ).toBe("O-001");
  });

  test("rejects an empty order", () => {
    expectErrorCode(() => inventory.createOrder([]), "EMPTY_ORDER");
    expect(inventory.listOrders()).toEqual([]);
  });

  test("rejects an unknown product", () => {
    expectErrorCode(
      () => inventory.createOrder([{ productId: "P-999", quantity: 1 }]),
      "PRODUCT_NOT_FOUND",
    );
    expect(inventory.listOrders()).toEqual([]);
  });

  test.each([0, -1, 1.5, Number.MAX_SAFE_INTEGER + 1, Number.NaN, Infinity])(
    "rejects invalid quantity %s",
    (quantity) => {
      expectErrorCode(
        () => inventory.createOrder([{ productId: "P-002", quantity }]),
        "INVALID_QUANTITY",
      );
      expect(inventory.listOrders()).toEqual([]);
    },
  );

  test("confirms an order and decrements stock", () => {
    const order = inventory.createOrder([
      { productId: "P-002", quantity: 2 },
      { productId: "P-003", quantity: 4 },
    ]);

    expect(inventory.confirmOrder(order.orderId).status).toBe("confirmed");
    expect(inventory.getAvailableQuantity("P-002")).toBe(3);
    expect(inventory.getAvailableQuantity("P-003")).toBe(6);
    expect(inventory.getOrder(order.orderId).status).toBe("confirmed");
  });

  test("keeps a failed confirmation atomic", () => {
    const order = inventory.createOrder([
      { productId: "P-002", quantity: 6 },
    ]);

    expectErrorCode(
      () => inventory.confirmOrder(order.orderId),
      "INSUFFICIENT_STOCK",
    );
    expect(inventory.getAvailableQuantity("P-002")).toBe(5);
    expect(inventory.getOrder(order.orderId).status).toBe("draft");
  });

  test("does not decrement an earlier product when a later product is insufficient", () => {
    const order = inventory.createOrder([
      { productId: "P-002", quantity: 2 },
      { productId: "P-001", quantity: 1 },
    ]);

    expectErrorCode(
      () => inventory.confirmOrder(order.orderId),
      "INSUFFICIENT_STOCK",
    );
    expect(inventory.getAvailableQuantity("P-002")).toBe(5);
    expect(inventory.getAvailableQuantity("P-001")).toBe(0);
    expect(inventory.getOrder(order.orderId).status).toBe("draft");
  });

  test("blocks a second confirmation without decrementing stock again", () => {
    const order = inventory.createOrder([
      { productId: "P-002", quantity: 2 },
    ]);
    inventory.confirmOrder(order.orderId);

    expectErrorCode(
      () => inventory.confirmOrder(order.orderId),
      "ORDER_ALREADY_CONFIRMED",
    );
    expect(inventory.getAvailableQuantity("P-002")).toBe(3);
  });

  test.each([
    ["read", (system: InventoryManagement) => system.getOrder("O-999")],
    ["confirm", (system: InventoryManagement) => system.confirmOrder("O-999")],
  ])("reports ORDER_NOT_FOUND when attempting to %s an unknown order", (_, operation) => {
    expectErrorCode(() => operation(inventory), "ORDER_NOT_FOUND");
  });

  test("deeply copies input lines and returned order snapshots", () => {
    const lines: OrderLine[] = [{ productId: "P-002", quantity: 2 }];
    const created = inventory.createOrder(lines);

    lines[0]!.quantity = 5;
    created.lines[0]!.quantity = 4;
    const read = inventory.getOrder(created.orderId);
    read.lines[0]!.quantity = 3;
    const listed = inventory.listOrders();
    listed[0]!.lines[0]!.quantity = 6;
    const confirmed = inventory.confirmOrder(created.orderId);
    confirmed.lines[0]!.quantity = 7;

    expect(inventory.getOrder(created.orderId).lines).toEqual([
      { productId: "P-002", quantity: 2 },
    ]);
  });

  test("keeps counters, orders, and stock isolated between instances", () => {
    const first = new InventoryManagement(initialProducts);
    const second = new InventoryManagement(initialProducts);
    const firstOrder = first.createOrder([
      { productId: "P-002", quantity: 2 },
    ]);
    const secondOrder = second.createOrder([
      { productId: "P-002", quantity: 1 },
    ]);

    first.confirmOrder(firstOrder.orderId);

    expect(firstOrder.orderId).toBe("O-001");
    expect(secondOrder.orderId).toBe("O-001");
    expect(first.getAvailableQuantity("P-002")).toBe(3);
    expect(second.getAvailableQuantity("P-002")).toBe(5);
    expect(second.getOrder(secondOrder.orderId).status).toBe("draft");
  });

  test("retains duplicate lines and confirms them when total stock is sufficient", () => {
    const order = inventory.createOrder([
      { productId: "P-002", quantity: 2 },
      { productId: "P-002", quantity: 3 },
    ]);

    expect(inventory.confirmOrder(order.orderId)).toEqual({
      orderId: "O-001",
      status: "confirmed",
      lines: [
        { productId: "P-002", quantity: 2 },
        { productId: "P-002", quantity: 3 },
      ],
    });
    expect(inventory.getAvailableQuantity("P-002")).toBe(0);
  });
});

function expectErrorCode(operation: () => unknown, expectedCode: string): void {
  expect(operation).toThrowError(expect.objectContaining({ code: expectedCode }));
}
