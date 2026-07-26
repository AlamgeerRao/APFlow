import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// APFlow.Web consumes APFlow.Api only over HTTP (per 03_Solution_Structure.md §2).
// No project references; the API base URL is supplied via environment configuration.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    // Pins the test process's timezone so formatDateTime's local-time
    // output (WP-020a; timeZone: 'UTC' deliberately dropped from
    // format.ts, flagged to the Architect for review — see
    // docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md §7a) is
    // deterministic across machines/CI, without hardcoding UTC back into
    // application code.
    env: { TZ: 'UTC' },
  },
});
