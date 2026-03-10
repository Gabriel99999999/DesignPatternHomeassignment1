# Performance Report (Template)

| Run Date | Input Size | Worker Count | Blur Radius | Operation | Avg Duration (ms) | Throughput (files/s) | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| _TBD_ | | | | | | | |

## Methodology

1. Warm up by executing `dotnet run --project WorkflowRunner.App -- <args>` once and discard metrics.
2. For each measured run, capture:
   - CLI metrics output (Queued/Started/Completed/Failed/Average duration).
   - Wall-clock start/end timestamps.
   - System load info (CPU %, memory).
3. Calculate throughput = `files processed / wall-clock seconds`.
4. Record any code/config changes between runs.

## Optimization Log

- _TBD_: document tuning experiments (e.g., worker count adjustments, processor optimizations) and link to supporting PRs or commits.
