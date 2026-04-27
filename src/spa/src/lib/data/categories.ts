export const CATEGORIES: { name: string; tests: string[] }[] = [
  {
    name: 'H/1.1 Isolated',
    tests: [
      'baseline-512', 'baseline-4096', 'baseline-16384',
      'limited-conn-512', 'limited-conn-4096',
      'json-4096', 'json-16384',
      'json-comp-512', 'json-comp-4096', 'json-comp-16384',
      'json-tls-4096',
      'upload-32', 'upload-64', 'upload-256', 'upload-512',
      'async-db-1024',
      'static-1024', 'static-4096', 'static-6800', 'static-16384',
      'pipelined-512', 'pipelined-4096', 'pipelined-16384',
      'crud-512', 'crud-4096',
      'mixed-4096',
    ],
  },
  {
    name: 'H/1.1 Workload',
    tests: ['api-4-256', 'api-16-1024'],
  },
  {
    name: 'H/2',
    tests: [
      'baseline-h2-256', 'baseline-h2-1024',
      'static-h2-256', 'static-h2-1024',
      'baseline-h2c-256', 'baseline-h2c-1024', 'baseline-h2c-4096',
      'json-h2c-1024', 'json-h2c-4096',
    ],
  },
  {
    name: 'H/3',
    tests: ['baseline-h3-64', 'static-h3-64'],
  },
  {
    name: 'Gateway',
    tests: [
      'gateway-64-256', 'gateway-64-512', 'gateway-64-1024',
      'gateway-h3-64', 'gateway-h3-256',
      'production-stack-256', 'production-stack-1024',
    ],
  },
  {
    name: 'gRPC',
    tests: [
      'unary-grpc-256', 'unary-grpc-1024',
      'unary-grpc-tls-256', 'unary-grpc-tls-1024',
      'stream-grpc-64', 'stream-grpc-tls-64',
    ],
  },
  {
    name: 'WebSocket',
    tests: ['echo-ws-512', 'echo-ws-4096', 'echo-ws-16384'],
  },
]
