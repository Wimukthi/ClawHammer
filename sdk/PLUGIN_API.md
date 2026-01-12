# ClawHammer Plugin API

This document describes the public plugin contract in `ClawHammer.PluginContracts`.

## IStressPlugin
Required for every workload. One assembly can define multiple plugins.

Properties:
- `Id`: Unique workload id (use reverse-DNS style like `com.example.myplugin`).
- `DisplayName`: Friendly name shown in the UI.
- `Description`: Short description for the UI.
- `Category`: Group label in the UI.
- `SortOrder`: Lower numbers sort first.
- `IsAdvanced`: Optional flag (currently not used in UI).
- `SupportsValidation`: Set to `True` if your worker emits validation status.

Method:
- `CreateWorker(workerId, seed, context)`: return an `IStressWorker` instance.

## IStressWorker
Per-thread worker class.

Properties:
- `KernelName`: Short label for the inner loop (used in Validation Monitor).

Method:
- `Run(token, reportProgress, validation, reportError, reportStatus)`
  - `token`: cancel when requested.
  - `reportProgress`: call with an integer count of operations to update throughput.
  - `validation`: `ValidationSettings` (may be `Nothing`).
  - `reportError`: call to signal validation failure (triggers auto-stop).
  - `reportStatus`: call to update validation monitor (see format below).

## StressPluginContext
Context values supplied by ClawHammer:
- `TotalWorkers`: total active threads for the run.
- `PrimeRangeMin` / `PrimeRangeMax`: range for prime-based validations.
- `MemoryBufferBytes`: suggested buffer size for memory workloads.
- `AvxSupported`: `True` if SIMD acceleration is available.

## ValidationSettings
- `Mode`: `Off`, `Light`, or `Full`.
- `IntervalMs`: minimum time between validation checks.
- `BatchSize`: suggested batch size for validation chunks.
- `RecordError(message)`: record a validation error (also increments error count).

## Validation status format
If you want per-thread status in the Validation Monitor, use:
`STATUS|<workerId>|<kernelName>|<detail>`

Example:
`STATUS|2|Prime Sweep|Tick OK (primes<=25000000)`

## Threading and performance guidelines
- Keep the inner loop tight and avoid allocations.
- Check `token.IsCancellationRequested` frequently.
- Use `reportProgress` in batches (e.g. every few thousand ops).
- Only run validation when `validation.Mode` is not `Off`.
