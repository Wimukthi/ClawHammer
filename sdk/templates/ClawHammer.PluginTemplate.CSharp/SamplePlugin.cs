using System;
using System.Threading;
using ClawHammer.PluginContracts;

namespace ClawHammer.PluginTemplate;

public sealed class SamplePlugin : IStressPlugin
{
    public string Id => "com.example.sample";
    public string DisplayName => "Sample Plugin";
    public string Description => "Demonstrates a minimal ClawHammer stress plugin.";
    public string Category => "Sample";
    public int SortOrder => 1000;
    public bool IsAdvanced => false;
    public bool SupportsValidation => true;

    public IStressWorker CreateWorker(int workerId, ulong seed, StressPluginContext context)
        => new SampleWorker(workerId, seed, context);
}

internal sealed class SampleWorker : IStressWorker
{
    private readonly int _workerId;
    private readonly StressPluginContext _context;
    private double _state;

    public SampleWorker(int workerId, ulong seed, StressPluginContext context)
    {
        _workerId = workerId;
        _context = context;
        _state = (seed % 1000UL) / 3.0 + 1.0;
    }

    public string KernelName => "Sample Loop";

    public void Run(
        CancellationToken token,
        Action<int>? reportProgress,
        ValidationSettings? validation,
        Action<string>? reportError,
        Action<string>? reportStatus)
    {
        int ops = 0;
        long lastValidationTick = Environment.TickCount64;

        int totalWorkers = _context?.TotalWorkers ?? 1;
        reportStatus?.Invoke($"STATUS|{_workerId}|{KernelName}|Init (workers={totalWorkers})");

        while (!token.IsCancellationRequested)
        {
            _state = Math.Sin(_state) + Math.Sqrt(_state + 1.0);
            ops++;

            if (ops >= 10000)
            {
                reportProgress?.Invoke(ops);
                ops = 0;
            }

            if (validation is { Mode: not ValidationMode.Off })
            {
                long nowTick = Environment.TickCount64;
                if (nowTick - lastValidationTick >= validation.IntervalMs)
                {
                    lastValidationTick = nowTick;

                    if (double.IsNaN(_state) || double.IsInfinity(_state))
                    {
                        validation.RecordError("Validation failed: non-finite value.");
                        reportError?.Invoke("Validation failed: non-finite value.");
                        return;
                    }

                    string detail = $"Tick OK (state={_state:F2})";
                    reportStatus?.Invoke($"STATUS|{_workerId}|{KernelName}|{detail}");
                }
            }
        }
    }
}
