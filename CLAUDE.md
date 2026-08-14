# HTTP Arena Timeline

The goal of this project is to scrape the benchmark results of HttpArena so they can be shown in a timeline view in an interactive SPA app. This allows maintainers and framework users to view the changes to the performance of a framework over time (in the test categories that HttpArena defines).

The technical solution consists of two parts:
* An import process that scrapes the data from the locally checked out GitHub repository of HttpArena and generates static JSON files containing the timeline data
* A SPA that uses this data to display timeline graphs (first selecting a framework, then selecting a test)

## Import Process

The import process should be a C# solution that can be executed via `dotnet` to go through the git history of the locally checked out HttpArena repository (`../HttpArena/`) and parses the result files that are stored in the following hierarchy in the sub folder `./site/data/`:

*testname-concurrentconnections.json*
```json
[
  {
    "framework": "actix",
    "language": "Rust",
    "rps": 3074,
    "avg_latency": "10.35ms",
    "p99_latency": "32.60ms",
    "cpu": "1929.4%",
    "memory": "175MiB",
    "connections": 32,
    "threads": 64,
    "duration": "5s",
    "pipeline": 1,
    "bandwidth": "414.96KB/s",
    "input_bw": "24.38GB/s",
    "reconnects": 3082,
    "status_2xx": 15403,
    "status_3xx": 0,
    "status_4xx": 0,
    "status_5xx": 0
  },
  ...
]
```

The `current.json` contains additional meta data about the run. Example:
```json
{
  "date": "2026-04-24",
  "cpu": "AMD Ryzen Threadripper PRO 3995WX 64-Cores",
  "cores": "64",
  "threads": "128",
  "threads_per_core": "2",
  "ram": "251Gi",
  "os": "Ubuntu 24.04.4 LTS",
  "kernel": "6.17.0-22-generic",
  "docker": "29.3.0",
  "docker_runtime": "runc",
  "governor": "performance",
  "commit": "30f0c0b5",
  "tcp": {
    "lo_mtu": "1500",
    "congestion": "cubic",
    "somaxconn": "65535",
    "rmem_max": "7500000",
    "wmem_max": "7500000"
  }
}
```

The dotnet solution now has to start at a specific checkout (use `8462fe848111505d304c69359d64c76e650dde5b` for the beginning, that might change later — this is the default baked into `src/Importer/Program.cs` and is the authoritative starting point, not necessarily what's written here), and scrape the result data into the following structure in the `data` folder of this repository:

* `<frameworkname>/`
  * `<testname>-<concurrency>.json`

The structure of the json files should look like this:
```json
{
  "data": [
    ["2025-01-01T10:22:00Z", { "rps": 43000, "avg_latency": "...", ... }],
    ["2025-01-02T10:22:00Z", { "rps": 43500, "avg_latency": "...", ... }],
    ["2025-01-03T10:22:00Z", { "rps": 43322, "avg_latency": "...", ... }]
  ]
}
```

It is important to keep all data points for later (so all measured fields). If there are multiple result records per day (due to multiple commits), keep them with the commit timestamp.

### Import modes

`data/state.json` tracks the last processed HttpArena commit. If it exists, `TimelineImporter` runs **incrementally** (only walks commits after that SHA). If it's missing, it runs a **full import** starting from `startingCommit`, which also snapshots the full `site/data` tree at that commit so tests unchanged since the start are still captured. To force a full re-import (e.g. after adding a test that predates the last incremental run, or to re-run since HttpArena history), delete `data/state.json` before running the importer — this also wipes and regenerates every `data/<framework>/` directory. A full re-import can resurrect frameworks/data that a maintainer previously pruned by hand from `data/`; that's expected if the framework is still enabled upstream in HttpArena, not a bug.

### Adding a new test to the timeline

HttpArena result files use `<test>-<concurrency>` keys (e.g. `fortunes-1024`, `static-tls-4096`). To surface a new one end-to-end:

1. Confirm the exact key(s) and concurrency by grepping `../HttpArena/site/data/results/*.json` for the test name — concurrency isn't guessable from the test name alone.
2. Add the key(s) to `AllowedTests` in `src/Importer/Services/TimelineImporter.cs` — tests not in this set are silently skipped during import and never reach `data/` or `index.json`.
3. Add the key(s) to the matching category's `tests` array in `src/spa/src/lib/data/categories.ts`. Check HttpArena's own docs under `../HttpArena/site/content/docs/test-profiles/` to see which category (H/1.1 Isolated, H/2, gRPC, etc.) a new test belongs to.
4. Re-run the importer (full re-import if the test predates the current `data/state.json` checkpoint).

## SPA

The SPA is a website with technology of your choice that uses the data in `./data` to display graphs per selected framework and test. There might be multiple functions and sections
in the future, but for now, the user can:

1. Select a framework (e.g. `genhttp`)
2. Select a test (e.g. `json-tls-4096`)

and see the changes of the metrics over time. All of the supported metrics can be selected, but by default only RPS is enabled.

Keep in mind that the website will be hosted on GitHub Pages, so we cannot use additional infrastructure such as database servers or anything else. 