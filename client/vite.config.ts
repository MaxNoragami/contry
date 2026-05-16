import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'

const devApiTarget = 'http://localhost:5087'

// https://vite.dev/config/
export default defineConfig({
  plugins: [svelte()],
  build: {
    rolldownOptions: {
      checks: {
        pluginTimings: false,
      },
    },
  },
  server: {
    allowedHosts: ['.ngrok-free.app'],
    proxy: {
      '/users': devApiTarget,
      '/sessions': devApiTarget,
      '/tokens': devApiTarget,
      '/xsrf': devApiTarget,
      '/ranked': devApiTarget,
      '/leaderboards': devApiTarget,
      '/datasets': devApiTarget,
      '/health': devApiTarget,
      '/test-records': devApiTarget,
    },
  },
})
