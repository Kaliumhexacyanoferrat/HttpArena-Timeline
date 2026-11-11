# HttpArena Timeline

A historical view of [HTTP Arena](https://www.http-arena.com) benchmark results, allowing framework maintainers and users to track performance changes over time.

**[Open the Timeline →](https://timeline.http-arena.com/)**

---

![Timeline screenshot showing RPS over time for genhttp on the crud-4096 test](docs/screenshot.png)

---

## What is this?

[HTTP Arena](https://www.http-arena.com) runs regular benchmarks across dozens of web frameworks and languages, measuring throughput, latency, CPU usage, memory consumption, and more. The results on the site reflect only the most recent run. This project fills the gap by recording every benchmark run into a time series, making it possible to answer questions like:

- Did this release improve throughput?
- When did latency regress, and by how much?
- How does a framework trend compared to its historical baseline?
- How does it compare to the field: the median, average, best or worst of all frameworks, or of its own language?

## How it works

The project has two components:

**Import process** — a C# tool that walks the git history of the [HTTP Arena repository](https://github.com/danielHalan/HttpArena) and extracts benchmark result files from each relevant commit. Results are written as static JSON files under `data/<framework>/<test>-<concurrency>.json`, deduplicated so that only runs with a meaningful RPS change (or a gap of 7+ days) are recorded.

It also computes **indicators**: the median, average, min and max of every metric across all frameworks and per language, snapshotted once a day for each test. They are written to `data/indicators/<scope>/<test>-<concurrency>.json`, where the scope is `all` or a language key such as `rust` or `csharp`.

**SPA** — a client-side web application that reads the static JSON files and renders interactive time-series graphs. Select a test and one or more frameworks, add indicators to compare against, and explore how any metric has evolved over time.

Because everything is static JSON served from GitHub Pages, no backend infrastructure is required.

## Data format

Each timeline file has the following structure:

```json
{
  "data": [
    ["2025-01-01T10:22:00Z", { "rps": 43000, "avg_latency_ms": 1.2, "p99_latency_ms": 3.5, ... }],
    ["2025-01-08T10:22:00Z", { "rps": 43500, "avg_latency_ms": 1.1, "p99_latency_ms": 3.3, ... }]
  ]
}
```

An `index.json` at the root of `data/` lists all available frameworks, tests and indicator scopes for the SPA to enumerate.

Indicator files are columnar to stay small: each point holds one value per entry of `metrics`, the number of frameworks `n` it covers, and for min/max the index into `frameworks` of the framework that set the value (`null` when all tied).

```json
{
  "metrics": ["rps", "avg_latency_ms", ...],
  "frameworks": ["actix", "h2o", ...],
  "data": [
    ["2026-08-15T18:22:00Z", { "n": 101, "median": [1456838.5, 2.73, ...], "avg": [...], "min": [...], "max": [...], "min_fw": [0, 1, ...], "max_fw": [...] }]
  ]
}
```

## Running the import locally

```bash
dotnet run --project src/Importer/Importer.csproj [<repo-path> [<output-path> [<starting-commit>]]]
```

Defaults point to `../HttpArena` as the source repository and `./data` as the output directory.

## License

MIT
