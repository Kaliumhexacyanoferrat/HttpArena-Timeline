import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'
import { resolve, join } from 'path'
import { createReadStream, existsSync, statSync } from 'fs'

const dataDir = resolve(__dirname, '../../data')

export default defineConfig({
  plugins: [
    svelte(),
    {
      // Serve the repo's data/ directory at /data/ during development.
      name: 'serve-data',
      configureServer(server) {
        server.middlewares.use('/data', (req, res, next) => {
          const filePath = join(dataDir, decodeURIComponent(req.url ?? '/'))
          if (existsSync(filePath) && statSync(filePath).isFile()) {
            res.setHeader('Content-Type', 'application/json')
            createReadStream(filePath).pipe(res as NodeJS.WritableStream)
          } else {
            next()
          }
        })
      }
    }
  ],
  base: process.env.BASE_PATH ?? '/',
})
