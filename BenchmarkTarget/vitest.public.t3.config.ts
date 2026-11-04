import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["tests/public/t3/confirm-duplicate-order-lines.test.ts"],
    exclude: ["tests/reserved/**"],
  },
});
