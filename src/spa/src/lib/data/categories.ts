/**
 * The tests of the timeline, grouped and ordered like HttpArena's board (its scopes H/1.1, H/2,
 * H/3, Gateway, gRPC, WebSocket and Database; scripts/gen_leaderboard_data.py, PROFILES).
 * Every concrete "<test>-<connections>" key in data/index.json belongs in exactly one category.
 */
export interface Category {
  name: string
  /** The board's short scope name, for tight spots. */
  short: string
  tests: string[]
}

export const CATEGORIES: Category[] = [
  {
    name: 'HTTP/1.1',
    short: 'H/1.1',
    tests: [
      'baseline-4096', 'pipelined-4096', 'limited-conn-4096',
      'async-32000',
      'connections-1m-1000000', 'connections-1m-tls-1000000', 'connections-5m-5000000',
      'latency-10k-1024', 'latency-1m-1024',
      'json-comp-4096', 'json-comp-16384', 'json-tls-4096',
      '8gbit-512',
      'static-tls-1024',
    ],
  },
  {
    name: 'HTTP/2',
    short: 'H/2',
    tests: [
      'baseline-h2-256', 'baseline-h2-1024',
      'static-h2-256', 'static-h2-1024',
      'baseline-h2c-256', 'baseline-h2c-1024', 'baseline-h2c-4096',
      'json-h2c-1024', 'json-h2c-4096',
    ],
  },
  {
    name: 'HTTP/3',
    short: 'H/3',
    tests: ['baseline-h3-64', 'static-h3-64'],
  },
  {
    name: 'Gateway',
    short: 'GW',
    tests: [
      'gateway-64-512', 'gateway-64-1024',
      'gateway-h3-64', 'gateway-h3-256',
      'production-stack-256', 'production-stack-1024',
    ],
  },
  {
    name: 'gRPC',
    short: 'gRPC',
    tests: [
      'unary-grpc-256', 'unary-grpc-1024',
      'unary-grpc-tls-256', 'unary-grpc-tls-1024',
    ],
  },
  {
    name: 'WebSocket',
    short: 'WS',
    tests: [
      'echo-ws-512', 'echo-ws-4096', 'echo-ws-16384',
      'echo-ws-pipeline-512', 'echo-ws-pipeline-4096', 'echo-ws-pipeline-16384',
      'echo-ws-limited-512', 'echo-ws-limited-4096',
    ],
  },
  {
    name: 'Database',
    short: 'DB',
    tests: ['async-db-1024', 'fortunes-1024'],
  },
]

export interface Profile {
  /** The board's column name. */
  label: string
  description: string
  /** Its page in HttpArena's Knowledge Base, under /docs/. */
  doc: string
}

/**
 * Per base test (the key without its connection count), what the board calls it. Optional:
 * a test without an entry shows its key.
 */
export const PROFILES: Record<string, Profile> = {
  'baseline':           { label: 'Baseline',         doc: 'test-profiles/h1/baseline',         description: 'Mixed GET/POST with query parsing.' },
  'pipelined':          { label: 'Pipelined',        doc: 'test-profiles/h1/pipelined',        description: '16x batched HTTP/1.1 pipelining (reference).' },
  'limited-conn':       { label: 'Short-lived',      doc: 'test-profiles/h1/short-lived',      description: 'Connections close after 10 requests.' },
  'async':              { label: 'Async Delay',      doc: 'test-profiles/h1/async',            description: 'A 10ms wait named in the route, at 32K held connections.' },
  'connections-1m':     { label: 'CON-1M',           doc: 'test-profiles/h1/connections',      description: 'One million held keep-alive connections at 250K req/s, scored on CPU per request, memory, p50 and p99 latency.' },
  'connections-1m-tls': { label: 'CON-1M-TLS',       doc: 'test-profiles/h1/connections',      description: 'One million TLS 1.3 sessions held at 250K req/s, scored on CPU per request, memory, p50 and p99 latency.' },
  'connections-5m':     { label: 'CON-5M',           doc: 'test-profiles/h1/connections',      description: 'Five million held connections at 1.25M req/s (reference, run for an allowlist of entries).' },
  'latency-10k':        { label: 'Latency-10K',      doc: 'test-profiles/h1/latency-10k',      description: 'CPU, mean and p99 latency at a near-idle 10K req/s.' },
  'latency-1m':         { label: 'Latency-1M',       doc: 'test-profiles/h1/latency-1m',       description: 'CPU, mean and p99 latency at a pinned 1M req/s.' },
  'json-comp':          { label: 'JSON Comp',        doc: 'test-profiles/h1/json-compressed',  description: 'gzip/brotli content negotiation.' },
  'json-tls':           { label: 'JSON TLS',         doc: 'test-profiles/h1/json-tls',         description: 'JSON over HTTP/1.1 + TLS.' },
  '8gbit':              { label: '8Gbit',            doc: 'test-profiles/h1/8gbit',            description: 'CPU, mean and p99 latency at a pinned 50K req/s, 10 KB echoed both ways.' },
  'static-tls':         { label: 'Static TLS',       doc: 'test-profiles/h1/static-tls',       description: '20-file static serving over TLS (reference for frameworks).' },
  'async-db':           { label: 'Async DB',         doc: 'test-profiles/h1/async-database',   description: 'Async Postgres sequential scan.' },
  'fortunes':           { label: 'Fortunes',         doc: 'test-profiles/h1/fortunes',         description: 'DB query + HTML template render.' },
  'baseline-h2':        { label: 'Baseline',         doc: 'test-profiles/h2/baseline-h2',      description: 'Baseline over h2 (TLS, ALPN).' },
  'static-h2':          { label: 'Static',           doc: 'test-profiles/h2/static-h2',        description: 'Static assets over h2 multiplexing.' },
  'baseline-h2c':       { label: 'Baseline (h2c)',   doc: 'test-profiles/h2/baseline-h2c',     description: 'Baseline over cleartext h2.' },
  'json-h2c':           { label: 'JSON (h2c)',       doc: 'test-profiles/h2/json-h2c',         description: 'JSON over cleartext h2.' },
  'baseline-h3':        { label: 'Baseline',         doc: 'test-profiles/h3/baseline-h3',      description: 'Baseline over QUIC + TLS 1.3.' },
  'static-h3':          { label: 'Static',           doc: 'test-profiles/h3/static-h3',        description: 'Static assets over QUIC.' },
  'unary-grpc':         { label: 'Unary',            doc: 'test-profiles/grpc/unary',          description: 'Unary gRPC over plaintext h2.' },
  'unary-grpc-tls':     { label: 'Unary TLS',        doc: 'test-profiles/grpc/unary',          description: 'Unary gRPC over TLS.' },
  'gateway-64':         { label: 'Gateway (H2)',     doc: 'test-profiles/gateway/gateway-h2',  description: 'Reverse proxy + server, mixed h2.' },
  'gateway-h3':         { label: 'Gateway (H3)',     doc: 'test-profiles/gateway/gateway-h3',  description: 'Reverse proxy + server over h3.' },
  'production-stack':   { label: 'Production Stack', doc: 'test-profiles/gateway/production-stack', description: 'Edge + Redis + JWT auth + server.' },
  'echo-ws':            { label: 'Echo',             doc: 'test-profiles/ws/echo',             description: 'WebSocket echo throughput.' },
  'echo-ws-pipeline':   { label: 'Echo Pipelined',   doc: 'test-profiles/ws/echo-pipeline',    description: 'Batched WebSocket echo.' },
  'echo-ws-limited':    { label: 'Echo Short-lived', doc: 'test-profiles/ws/echo-limited',     description: 'WebSocket echo, 10 messages per connection.' },
}

/** "json-tls-4096" → { base: "json-tls", connections: 4096 } */
export function splitTest(test: string): { base: string; connections: number | null } {
  const m = /^(.*)-(\d+)$/.exec(test)
  return m ? { base: m[1], connections: Number(m[2]) } : { base: test, connections: null }
}

export function profileOf(test: string): Profile | undefined {
  return PROFILES[splitTest(test).base]
}

export function categoryOf(test: string): Category | undefined {
  return CATEGORIES.find(c => c.tests.includes(test))
}

/** 4096 → "4,096", 1000000 → "1M", 5000000 → "5M" */
export function formatConnections(n: number): string {
  if (n >= 1_000_000 && n % 1_000_000 === 0) return `${n / 1_000_000}M`
  return n.toLocaleString('en-US')
}
