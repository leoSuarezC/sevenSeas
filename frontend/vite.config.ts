/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],

  // .env lives at the repo root, next to .env.example, so there is one place to configure
  // both halves of the app rather than one per folder to keep in sync.
  envDir: '..',

  server: {
    port: 5173,
  },
  test: {
    // The component reads the rendered DOM, so it needs a document.
    environment: 'jsdom',
    include: ['src/**/*.spec.ts'],
  },
})
