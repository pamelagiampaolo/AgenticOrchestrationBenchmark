import { defineConfig } from "vitest/config";

const compatibleCommonTestNames = [
  "lists only product data ordered by product identifier",
  "returns the available quantity for P-001",
  "returns the available quantity for P-002",
  "returns the available quantity for P-003",
  "exposes a stable error code for an unknown product",
  "copies initial and returned product data",
  "keeps data independent between instances",
  "lists orders by textual identifier",
  "does not consume an identifier when creation fails",
  "rejects an empty order",
  "rejects an unknown product",
  "rejects invalid quantity 0",
  "rejects invalid quantity -1",
  "rejects invalid quantity 1.5",
  "rejects invalid quantity 9007199254740992",
  "rejects invalid quantity NaN",
  "rejects invalid quantity Infinity",
  "blocks a second confirmation without decrementing stock again",
  "reports ORDER_NOT_FOUND when attempting to read an unknown order",
  "reports ORDER_NOT_FOUND when attempting to confirm an unknown order",
  "keeps counters, orders, and stock isolated between instances",
];

const escapedCommonTestNames = compatibleCommonTestNames.map((name) =>
  name.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"),
);

export default defineConfig({
  test: {
    environment: "node",
    include: [
      "tests/public/inventory-management.test.ts",
      "tests/public/order-management.test.ts",
      "tests/public/t6/partial-fulfillment.test.ts",
    ],
    exclude: ["tests/reserved/**"],
    testNamePattern: new RegExp(
      `(?: > (?:${escapedCommonTestNames.join("|")})$| > T6-P0[1-5]\\b)`,
    ),
  },
});
