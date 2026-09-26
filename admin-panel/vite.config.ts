/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    // Gelistirmede /api isteklerini .NET API'ye yonlendir: tarayici ayni origin'e istek
    // attigini dusunur, CORS ile ugrasmayiz.
    proxy: {
      '/api': 'http://localhost:5182',
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
