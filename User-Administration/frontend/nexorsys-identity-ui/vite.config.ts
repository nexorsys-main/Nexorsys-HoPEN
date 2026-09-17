import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import path from "node:path";
import { fileURLToPath } from "node:url";

const configDirectory = path.dirname(fileURLToPath(import.meta.url));

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 3005,
    host: true,
    proxy: {
      "/api": {
        target: "http://localhost:5000",
        changeOrigin: true
      },
      "/hubs": {
        target: "http://localhost:5000",
        ws: true,
        changeOrigin: true
      }
    }
  },
  build: {
    outDir: "dist",
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            {
              name: "ui-vendor",
              test: /node_modules[\\/](antd|@ant-design|rc-[^\\/]+)/,
              maxSize: 300_000,
              minSize: 20_000,
              entriesAware: true,
              priority: 10,
            },
          ],
        },
      },
    },
  },
  publicDir: "public",
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: [],
  },
  resolve: {
    alias: {
      "toggle-selection": path.resolve(configDirectory, "src/shims/toggle-selection.ts"),
    },
  },
});
