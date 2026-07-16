/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
  },
  test: {
    // The component reads the rendered DOM, so it needs a document.
    environment: 'jsdom',
    include: ['src/**/*.spec.ts'],
  },
})
