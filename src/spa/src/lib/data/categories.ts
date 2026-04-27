export const CATEGORIES: { name: string; tests: string[] }[] = [
  {
    name: 'H/1.1 Isolated',
    tests: [
      'baseline-4096', 'baseline-512',
      'limited-conn-4096', 'limited-conn-512',
      'json-4096',
      'json-comp-4096', 'json-comp-512', 'json-comp-16384',
      'json-tls-4096',
      'upload-256', 'upload-32',
      'async-db-1024',
      'static-4096', 'static-1024', 'static-6800',
      'pipelined-4096', 'pipelined-512',
      'crud-4096',
    ],
  },
  {
    name: 'H/1.1 Workload',
    tests: ['api-4-256', 'api-16-1024'],
  },
  {
    name: 'H/2',
    tests: [
      'baseline-h2-1024', 'baseline-h2-256',
      'static-h2-1024', 'static-h2-256',
      'baseline-h2c-1024', 'baseline-h2c-256', 'baseline-h2c-4096',
      'json-h2c-1024', 'json-h2c-4096',
    ],
  },
  {
    name: 'H/3',
    tests: ['baseline-h3-64', 'static-h3-64'],
  },
  {
    name: 'WebSocket',
    tests: ['echo-ws-512', 'echo-ws-4096', 'echo-ws-16384'],
  },
]
