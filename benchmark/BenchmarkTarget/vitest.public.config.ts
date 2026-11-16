import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: [
      "tests/public/inventory-management.test.ts",
      "tests/public/order-management.test.ts",
    ],
    exclude: ["tests/reserved/**"],
  },
});
