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

The dotnet solution now has to start at a specific checkout (use `756e9ddf7e4bbf357f0504bca31cce398d089042` for the beginning, that might change later), and scrape the result data into the following structure in the `data` folder of this repository:

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

## SPA

The SPA is a website with technology of your choice that uses the data in `./data` to display graphs per selected framework and test. There might be multiple functions and sections
in the future, but for now, the user can:

1. Select a framework (e.g. `genhttp`)
2. Select a test (e.g. `json-tls-4096`)

and see the changes of the metrics over time. All of the supported metrics can be selected, but by default only RPS is enabled.

Keep in mind that the website will be hosted on GitHub Pages, so we cannot use additional infrastructure such as database servers or anything else. 