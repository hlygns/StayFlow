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
      // SignalR: WebSocket baglantisi da ayni sekilde API'ye gecsin
      '/hubs': { target: 'http://localhost:5182', ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
