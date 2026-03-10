# Performance Report (Template)

| Run Date | Input Size | Worker Count | Blur Radius | Operation | Avg Duration (ms) | Throughput (files/s) | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2026-03-11 | 100 JPGs (default `/input`) | 2 | 5 | Blur | 27.9 | 62.9 | Baseline run, `dotnet run --no-build -- input output 5 2 blur`, wall clock 1.59 s. |
| 2026-03-11 | 100 JPGs (same set) | 4 | 5 | Blur | 27.3 | 63.7 | Optimization attempt: doubled workers, slight throughput gain; note diminishing returns because processors are CPU-bound. |

## Methodology

1. Warm up by executing `dotnet run --project WorkflowRunner.App -- <args>` once and discard metrics.
2. For each measured run, capture:
   - CLI metrics output (Queued/Started/Completed/Failed/Average duration).
   - Wall-clock start/end timestamps.
   - System load info (CPU %, memory).
3. Calculate throughput = `files processed / wall-clock seconds`.
4. Record any code/config changes between runs.

## Optimization Log

- 2026-03-11: Compared 2 vs 4 workers; throughput improved ~1.2 files/s, indicating IO/CPU mix rather than pure queue contention. Future optimizations should focus on processor SIMD usage instead of simply raising worker count.
