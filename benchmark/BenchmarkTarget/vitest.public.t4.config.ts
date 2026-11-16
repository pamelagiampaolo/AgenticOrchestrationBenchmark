import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["tests/public/t4/product-suspension.test.ts"],
    exclude: ["tests/reserved/**"],
  },
});
