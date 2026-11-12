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

### Import model (always a full rebuild)

The importer **always rebuilds `data/` from scratch** — there is no incremental mode and no `data/state.json`. On every run it wipes and regenerates every `data/<framework>/` directory, then rebuilds `index.json`. This is deliberate: the set of active framework/test combinations can change in ways only a full rebuild reflects (a framework being disabled upstream must retroactively drop its whole directory, not just stop growing).

**Source of truth for what is kept** is HttpArena's *current main*, read at startup by `ActiveConfig` (`src/Importer/Services/ActiveConfig.cs`) from two files on main's tip:
* `scripts/lib/profiles.sh` — maps each base test to its concurrency level(s) (`echo-ws` → 512,4096,16384), producing the concrete `<test>-<conns>` keys.
* `frameworks/<dir>/meta.json` — per framework, whether it is `enabled` and which base tests it runs. Several variant folders can share one `display_name` (e.g. `actix`, `actix-h2c`, `actix-websocket` → `actix`); their test lists are **unioned**, matching how results merge under one framework name.

Their cross-product is the set of active `(framework, <test>-<conns>)` combos. While walking history, a data point is kept only if its combo is active *now*. Consequences worth knowing:
* Disabled frameworks (`enabled: false`) and retired tests are dropped even though the HttpArena `results/*.json` files still contain stale rows for them (which is exactly why `meta.json`/`profiles.sh` are the truth, not the results files).
* A framework enabled on main but with no historical data yet simply produces no directory until data exists.
* There is no hand-pruning of `data/`; the active set fully determines the output.

### Indicators (field aggregates)

**Indicators** are the median, average, min and max of every SPA metric per test, across all frameworks (scope `all`) or per language (scope = language slug, e.g. `rust`, `csharp`, `cpp`; see `IndicatorBuilder.LanguageSlug`), over the frameworks HttpArena's type filter selects. Because the filter is user-selectable, the work is split in two:
* The importer (`src/Importer/Services/IndicatorBuilder.cs`) writes one **field log** per test, `data/indicators/<test>-<conns>.json`: per UTC day, the rows that changed and the frameworks removed. Scopes are listed in `index.json` under `indicators`; each framework there carries `type` and `tuned`. `indicators` is a reserved folder name; the importer fails if a framework would map to it.
* The SPA (`src/spa/src/lib/data/field.ts`, `computeIndicator`) replays the log and aggregates the frameworks passing the type filter and scope. It was verified to reproduce the former C# aggregation exactly (all 645 default-filter series, 11,193 points), so keep both halves of the semantics below in step.

How the values are derived — keep these semantics when changing it:
* The importer keeps a live "field": the latest row per (test, framework) as HttpArena showed it at that point in history. A newer version of a result file replaces that file's rows; a deleted file drops them. Rows remember their source file, so the per-test → per-framework layout switch (old files deleted in the same commit) never drops a row a newer file set.
* Only active combos are logged (same `ActiveConfig` filter as everything else). The log keeps rows with `rps <= 0` (a failed run replaces the previous row); the SPA excludes them when aggregating.
* Only frameworks passing the **type filter** count (see below).
* Classification must match HttpArena 1:1. `ActiveConfig` resolves it like HttpArena's `scripts/rebuild_site_data.py` builds the leaderboard's `frameworks.json`: every `meta.json` counts (enabled or not), entries are grouped by `display_name`, the primary entry (folder named like the display name, else the first folder in ordinal order) gives `type` (default `emerging`) and `mode` (default `standard`). Today's classification applies to the whole history, like the active set.
* Results are merged one framework PR at a time, so the field is recorded **once per UTC day** (timestamp = last data commit that day) for the tests touched that day, not per commit. Every touched day is logged, even without net change; the SPA skips unchanged aggregates unless 7+ days passed, like the framework files.
* Frameworks are aggregated in ordinal name order; min/max name the first framework with the extreme value (indices into the log's `frameworks`, first-seen order so appending history doesn't renumber). Ties where every framework has the same value name nobody. The average is summed in ascending value order.

In the SPA, indicators are selected via the "+ indicator" picker (`IndicatorPicker.svelte`), stored in the URL as `indicators=median.all,max.rust`, and drawn as step lines sharing the framework color palette.

The **type filter** sits in the page header like on the leaderboard (`TypeFilterBar.svelte`, rules in `src/spa/src/lib/data/classification.ts`): same buttons, colors and toggle rules (flagship/emerging/experimental combine, engine and infra are exclusive, never empty, separate tuned toggle). It narrows the framework picker and decides what the indicators aggregate. Default: flagship + emerging **without** tuned (`DEFAULT_SHOW_TUNED`); the board's own code defaults to showing tuned, so this is a deliberate difference. Like the board it is remembered in localStorage and, when not the default, written to the URL with the board's parameter names (`type=engine&tuned=0`).

### Adding a new test to the timeline

The importer picks up new tests automatically once they are active on HttpArena main (a base test in `profiles.sh` referenced by an enabled framework's `meta.json` `tests`), so **no importer code change is needed**. The only manual step is the UI grouping:

1. Add the concrete `<test>-<concurrency>` key(s) to the matching category's `tests` array in `src/spa/src/lib/data/categories.ts`. Check HttpArena's docs under `../HttpArena/site/content/docs/test-profiles/` (h1/h2/h3/gateway/grpc/ws) for the right category, and `scripts/lib/profiles.sh` for the concurrency level(s).
2. Re-run the importer. Keep `categories.ts` and `data/index.json` in agreement — every test in one should appear in the other.

## SPA

The SPA is a website with technology of your choice that uses the data in `./data` to display graphs per selected framework and test. There might be multiple functions and sections
in the future, but for now, the user can:

1. Select a framework (e.g. `genhttp`)
2. Select a test (e.g. `json-tls-4096`)
3. Optionally add indicators (e.g. median of all frameworks, max of Rust) to compare against
4. Optionally drag across the chart to filter a time range (whole UTC days, stored in the URL as `from`/`to`; shown as a removable chip after the indicators, double-click the chart to reset)
5. Optionally pick a time resolution (All, Day, Week, Month, Quarter, Year; URL param `res`). This is aggregated client-side in `TimelineChart.svelte` (`bucket`), not at ingestion: framework runs in a period are averaged; indicators are states, so median/avg are time-weighted over how long each value held and min/max take the extreme held in the period. Points sit at the period start (UTC); the chart axis is UTC throughout

and see the changes of the metrics over time. All of the supported metrics can be selected, but by default only RPS is enabled.

Keep in mind that the website will be hosted on GitHub Pages, so we cannot use additional infrastructure such as database servers or anything else. 