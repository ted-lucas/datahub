import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The .NET API listens on http://localhost:5275 (http profile) and
// https://localhost:7283 (https profile). See src/DataHub.Api/Properties/launchSettings.json.
// Override with VITE_API_PROXY_TARGET if you want to point at a different URL.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5275',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
