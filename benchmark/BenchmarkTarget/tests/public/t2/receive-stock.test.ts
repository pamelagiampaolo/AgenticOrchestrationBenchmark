import { beforeEach, describe, expect, test } from "vitest";

import {
  InventoryManagement,
  type InitialProduct,
} from "../../../src/index.js";

describe("T2 - receiveStock", () => {
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

  test("T2-P01 loads stock and changes only the selected product", () => {
    const stateBefore = readObservableState(inventory);

    expect(inventory.receiveStock("P-001", 3)).toBe(3);
    expect(inventory.getAvailableQuantity("P-001")).toBe(3);
    expect(readObservableState(inventory)).toEqual(
      withAvailableQuantity(stateBefore, "P-001", 3),
    );
  });

  test("T2-P02 sums successive loads and changes only the selected product", () => {
    const stateBefore = readObservableState(inventory);

    expect(inventory.receiveStock("P-002", 2)).toBe(7);
    expect(inventory.getAvailableQuantity("P-002")).toBe(7);
    expect(readObservableState(inventory)).toEqual(
      withAvailableQuantity(stateBefore, "P-002", 7),
    );
    expect(inventory.receiveStock("P-002", 4)).toBe(11);
    expect(inventory.getAvailableQuantity("P-002")).toBe(11);
    expect(readObservableState(inventory)).toEqual(
      withAvailableQuantity(stateBefore, "P-002", 11),
    );
  });

  test("T2-P03 rejects zero without changing observable state", () => {
    const stateBefore = readObservableState(inventory);

    expect(() => inventory.receiveStock("P-003", 0)).toThrowError(
      expect.objectContaining({ code: "INVALID_QUANTITY" }),
    );
    expect(readObservableState(inventory)).toEqual(stateBefore);
  });

  test("T2-P04 rejects an unknown product without changing observable state", () => {
    const stateBefore = readObservableState(inventory);

    expect(() => inventory.receiveStock("P-999", 1)).toThrowError(
      expect.objectContaining({ code: "PRODUCT_NOT_FOUND" }),
    );
    expect(readObservableState(inventory)).toEqual(stateBefore);
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

function withAvailableQuantity(
  state: ReturnType<typeof readObservableState>,
  productId: string,
  availableQuantity: number,
): ReturnType<typeof readObservableState> {
  return {
    ...state,
    availableQuantities: state.availableQuantities.map((stock) =>
      stock.productId === productId ? { productId, availableQuantity } : stock,
    ),
  };
}
