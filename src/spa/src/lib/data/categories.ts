export interface Category {
  name: string
  tests: string[]
  color: string
  bg: string
}

export const CATEGORIES: Category[] = [
  {
    name: 'H/1.1 Isolated',
    color: '#60a5fa',
    bg: 'rgba(59,130,246,0.15)',
    tests: [
      'baseline-4096',
      'limited-conn-4096',
      'json-comp-4096', 'json-comp-16384',
      'json-tls-4096',
      'async-32000',
      'async-db-1024',
      '8gbit-512',
      'latency-10k-1024', 'latency-1m-1024',
      'static-tls-1024',
      'pipelined-4096',
      'fortunes-1024',
    ],
  },
  {
    name: 'H/2',
    color: '#fbbf24',
    bg: 'rgba(234,179,8,0.15)',
    tests: [
      'baseline-h2-256', 'baseline-h2-1024',
      'static-h2-256', 'static-h2-1024',
      'baseline-h2c-256', 'baseline-h2c-1024', 'baseline-h2c-4096',
      'json-h2c-1024', 'json-h2c-4096',
    ],
  },
  {
    name: 'Gateway',
    color: '#fbbf24',
    bg: 'rgba(234,179,8,0.15)',
    tests: [
      'gateway-64-512', 'gateway-64-1024',
      'gateway-h3-64', 'gateway-h3-256',
      'production-stack-256', 'production-stack-1024',
    ],
  },
  {
    name: 'H/3',
    color: '#4ade80',
    bg: 'rgba(34,197,94,0.15)',
    tests: ['baseline-h3-64', 'static-h3-64'],
  },
  {
    name: 'gRPC',
    color: '#a78bfa',
    bg: 'rgba(124,58,237,0.15)',
    tests: [
      'unary-grpc-256', 'unary-grpc-1024',
      'unary-grpc-tls-256', 'unary-grpc-tls-1024',
    ],
  },
  {
    name: 'WebSocket',
    color: '#22d3ee',
    bg: 'rgba(8,145,178,0.15)',
    tests: [
      'echo-ws-512', 'echo-ws-4096', 'echo-ws-16384',
      'echo-ws-limited-512', 'echo-ws-limited-4096',
      'echo-ws-pipeline-512', 'echo-ws-pipeline-4096', 'echo-ws-pipeline-16384',
    ],
  },
]
