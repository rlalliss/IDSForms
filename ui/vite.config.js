import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  optimizeDeps: {
    include: ['pdfjs-dist/build/pdf', 'pdfjs-dist/web/pdf_viewer.mjs', 'pdfjs-dist/build/pdf.worker.min.mjs'],
  },
  server: {
    proxy: {
      // Proxy frontend /api requests to the ASP.NET Core backend
      '/api': {
        target: 'https://localhost:5001',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
