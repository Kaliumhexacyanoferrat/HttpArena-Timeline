\# HTTP Arena Timeline



The goal of this project is to scrape the benchmark results of HttpArena so they can be shown in a timeline view in an interactive SPA app. This allows

maintainers and framework users to view the changes to the performance of a framework over time (in the test categories that HttpArena defines).



The technical solution consists of two parts:



* An import process that scrapes the data from the locally checked out GitHub repository of HttpArena and generates static JSON files containing the timeline data
* A SPA that uses this data to display timeline graphs (first selecting a framework, then selecting a test)



\## Import Process



The import process should be a C# solution that can be executed via `dotnet` to go through the git history of the locally checked out HttpArena repository (../HttpArena/)

and parses the result files that are stored in the following hierarchy in the sub folder `./site/data/`:



\*testname-concurrentconnections.json\*



```json

\[

&#x20; {

&#x20;   "framework": "actix",

&#x20;   "language": "Rust",

&#x20;   "rps": 3074,

&#x20;   "avg\_latency": "10.35ms",

&#x20;   "p99\_latency": "32.60ms",

&#x20;   "cpu": "1929.4%",

&#x20;   "memory": "175MiB",

&#x20;   "connections": 32,

&#x20;   "threads": 64,

&#x20;   "duration": "5s",

&#x20;   "pipeline": 1,

&#x20;   "bandwidth": "414.96KB/s",

&#x20;   "input\_bw": "24.38GB/s",

&#x20;   "reconnects": 3082,

&#x20;   "status\_2xx": 15403,

&#x20;   "status\_3xx": 0,

&#x20;   "status\_4xx": 0,

&#x20;   "status\_5xx": 0

&#x20; },

&#x20; ...

]

```



The `current.json` contains additional meta data about the run. Example:



```json

{

&#x20; "date": "2026-04-24",

&#x20; "cpu": "AMD Ryzen Threadripper PRO 3995WX 64-Cores",

&#x20; "cores": "64",

&#x20; "threads": "128",

&#x20; "threads\_per\_core": "2",

&#x20; "ram": "251Gi",

&#x20; "os": "Ubuntu 24.04.4 LTS",

&#x20; "kernel": "6.17.0-22-generic",

&#x20; "docker": "29.3.0",

&#x20; "docker\_runtime": "runc",

&#x20; "governor": "performance",

&#x20; "commit": "30f0c0b5",

&#x20; "tcp": {

&#x20;   "lo\_mtu": "1500",

&#x20;   "congestion": "cubic",

&#x20;   "somaxconn": "65535",

&#x20;   "rmem\_max": "7500000",

&#x20;   "wmem\_max": "7500000"

&#x20; }

}

```



The dotnet solution now has to start at a specific checkout (use `756e9ddf7e4bbf357f0504bca31cce398d089042` for the beginning, that might change later), and scrape the result data

into the following structure in the `data` folder of this repository:



* <frameworkname>

  * <testname>-<concurrency>.json



The structure of the json files should look like this:



```json

{

&#x20; "data": \[

&#x20;   \["2025-01-01T10:22:00Z", { "rps": 43000, "avg\_latency": ..., ... } ],

&#x20;   \["2025-01-02T10:22:00Z", { "rps": 43500, "avg\_latency": ..., ... } ],

&#x20;   \["2025-01-03T10:22:00Z", { "rps": 43322, "avg\_latency": ..., ... } ]

&#x20; ]

}

```



It is important to keep all data points for later (so all measured fields). If there are multiple result records per day (due to multiple commits), keep them with the commit timestamp.



\## SPA



TBD



