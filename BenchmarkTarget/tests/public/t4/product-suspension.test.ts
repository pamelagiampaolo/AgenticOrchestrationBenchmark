import { beforeEach, describe, expect, test } from "vitest";

import {
  InventoryManagement,
  type InitialProduct,
} from "../../../src/index.js";

describe("T4 - product suspension", () => {
  let inventory: InventoryManagement;

  beforeEach(() => {
    const initialProducts: InitialProduct[] = [
      { productId: "P-001", name: "Tastiera", availableQuantity: 0 },
      { productId: "P-002", name: "Mouse", availableQuantity: 5 },
      { productId: "P-003", name: "Monitor", availableQuantity: 10 },
    ];
    inventory = new InventoryManagement(initialProducts);
  });

  test("T4-P01 suspends a product without changing the rest of the state", () => {
    const stateBefore = readObservableState(inventory);

    const suspended = inventory.suspendProduct("P-002");

    expect(suspended).toEqual({
      productId: "P-002",
      name: "Mouse",
      status: "suspended",
    });
    expect(inventory.getAvailableQuantity("P-002")).toBe(5);
    expect(readObservableState(inventory)).toEqual(
      withProductStatus(stateBefore, "P-002", "suspended"),
    );

    suspended.name = "Modificato";
    suspended.status = "active";
    expect(productWithId(inventory, "P-002")).toEqual({
      productId: "P-002",
      name: "Mouse",
      status: "suspended",
    });
  });

  test("T4-P02 blocks confirmation with a suspended product atomically", () => {
    inventory.suspendProduct("P-002");
    const order = inventory.createOrder([
      { productId: "P-002", quantity: 1 },
    ]);
    const stateBeforeConfirmation = readObservableState(inventory);

    expect(() => inventory.confirmOrder(order.orderId)).toThrowError(
      expect.objectContaining({ code: "PRODUCT_SUSPENDED" }),
    );
    expect(readObservableState(inventory)).toEqual(stateBeforeConfirmation);
    expect(inventory.getOrder(order.orderId).status).toBe("draft");
  });

  test("T4-P03 reactivates a product and allows its order to be confirmed", () => {
    const stateBeforeTransitions = readObservableState(inventory);
    const suspended = inventory.suspendProduct("P-002");
    expect(suspended).toEqual({
      productId: "P-002",
      name: "Mouse",
      status: "suspended",
    });

    const reactivated = inventory.reactivateProduct("P-002");
    expect(reactivated).toEqual({
      productId: "P-002",
      name: "Mouse",
      status: "active",
    });
    expect(readObservableState(inventory)).toEqual(stateBeforeTransitions);
    const order = inventory.createOrder([
      { productId: "P-002", quantity: 1 },
    ]);
    const stateBeforeConfirmation = readObservableState(inventory);

    const confirmed = inventory.confirmOrder(order.orderId);

    expect(confirmed).toEqual({
      orderId: "O-001",
      status: "confirmed",
      lines: [{ productId: "P-002", quantity: 1 }],
    });
    expect(inventory.getAvailableQuantity("P-002")).toBe(4);
    expect(readObservableState(inventory)).toEqual(
      afterConfirmation(stateBeforeConfirmation, order.orderId, "P-002", 4),
    );

    reactivated.name = "Modificato";
    reactivated.status = "suspended";
    expect(productWithId(inventory, "P-002")).toEqual({
      productId: "P-002",
      name: "Mouse",
      status: "active",
    });
  });

  test("T4-P04 rejects a repeated suspension without changing state", () => {
    inventory.suspendProduct("P-003");
    const stateBeforeSecondSuspension = readObservableState(inventory);

    expect(() => inventory.suspendProduct("P-003")).toThrowError(
      expect.objectContaining({ code: "PRODUCT_ALREADY_SUSPENDED" }),
    );
    expect(readObservableState(inventory)).toEqual(
      stateBeforeSecondSuspension,
    );
  });
});

function readObservableState(inventory: InventoryManagement) {
  const products = inventory.listProducts();

  return {
    products,
    productsWithStatus: inventory.listProductsWithStatus(),
    availableQuantities: products.map(({ productId }) => ({
      productId,
      availableQuantity: inventory.getAvailableQuantity(productId),
    })),
    orders: inventory.listOrders(),
  };
}

function productWithId(inventory: InventoryManagement, productId: string) {
  return inventory
    .listProductsWithStatus()
    .find((product) => product.productId === productId);
}

function withProductStatus(
  state: ReturnType<typeof readObservableState>,
  productId: string,
  status: "active" | "suspended",
): ReturnType<typeof readObservableState> {
  return {
    ...state,
    productsWithStatus: state.productsWithStatus.map((product) =>
      product.productId === productId ? { ...product, status } : product,
    ),
  };
}

function afterConfirmation(
  state: ReturnType<typeof readObservableState>,
  orderId: string,
  productId: string,
  availableQuantity: number,
): ReturnType<typeof readObservableState> {
  return {
    ...state,
    availableQuantities: state.availableQuantities.map((stock) =>
      stock.productId === productId ? { productId, availableQuantity } : stock,
    ),
    orders: state.orders.map((order) =>
      order.orderId === orderId ? { ...order, status: "confirmed" } : order,
    ),
  };
}
