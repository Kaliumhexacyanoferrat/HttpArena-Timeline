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
          const filePath = join(dataDir, decodeURIComponent((req.url ?? '/').split('?')[0]))
          if (existsSync(filePath) && statSync(filePath).isFile()) {
            res.setHeader('Content-Type', 'application/json')
            createReadStream(filePath).pipe(res as NodeJS.WritableStream)
          } else {
            res.statusCode = 404
            res.end()
          }
        })
      }
    }
  ],
  base: process.env.BASE_PATH ?? '/',
  define: {
    __BUILD_TIME__: JSON.stringify(Date.now()),
  },
})
