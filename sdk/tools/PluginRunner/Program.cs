using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using ClawHammer.PluginContracts;

static class Program
{
    public static int Main(string[] args)
    {
        if (!TryParseArgs(args, out string? pluginPath, out string? pluginId, out int seconds))
        {
            PrintUsage();
            return 1;
        }

        if (!File.Exists(pluginPath))
        {
            Console.WriteLine($"Plugin DLL not found: {pluginPath}");
            return 1;
        }

        try
        {
            RunPlugin(pluginPath, pluginId, seconds);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Runner failed: " + ex.Message);
            return 1;
        }
    }

    private static bool TryParseArgs(string[] args, out string? pluginPath, out string? pluginId, out int seconds)
    {
        pluginPath = null;
        pluginId = null;
        seconds = 10;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--plugin":
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }
                    pluginPath = args[++i];
                    break;
                case "--id":
                    if (i + 1 >= args.Length)
                    {
                        return false;
                    }
                    pluginId = args[++i];
                    break;
                case "--seconds":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out seconds))
                    {
                        return false;
                    }
                    seconds = Math.Max(1, seconds);
                    break;
                case "-h":
                case "--help":
                    return false;
            }
        }

        return !string.IsNullOrWhiteSpace(pluginPath);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("ClawHammer Plugin Runner");
        Console.WriteLine("Usage: dotnet run -- --plugin <path> [--id <pluginId>] [--seconds 10]");
    }

    private static void RunPlugin(string pluginPath, string? pluginId, int seconds)
    {
        string fullPath = Path.GetFullPath(pluginPath);
        Assembly asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);

        var pluginTypes = asm.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IStressPlugin).IsAssignableFrom(t))
            .ToList();

        if (pluginTypes.Count == 0)
        {
            throw new InvalidOperationException("No IStressPlugin implementations found.");
        }

        IStressPlugin? selected = null;
        foreach (Type type in pluginTypes)
        {
            if (Activator.CreateInstance(type) is not IStressPlugin plugin)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(plugin.Id))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(pluginId) || plugin.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase))
            {
                selected = plugin;
                break;
            }
        }

        if (selected is null)
        {
            throw new InvalidOperationException("No matching plugin id found.");
        }

        Console.WriteLine($"Running {selected.DisplayName} ({selected.Id}) for {seconds}s...");

        var context = new StressPluginContext(
            totalWorkers: 1,
            primeRangeMin: 2,
            primeRangeMax: 25000000,
            memoryBufferBytes: 4 * 1024 * 1024,
            avxSupported: Vector.IsHardwareAccelerated);

        IStressWorker worker = selected.CreateWorker(0, 1UL, context);

        long totalOps = 0;
        var validation = new ValidationSettings(ValidationMode.Light, intervalMs: 2000, batchSize: 4096);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));

        Action<int>? reportProgress = ops => Interlocked.Add(ref totalOps, ops);
        Action<string>? reportError = message =>
        {
            validation.RecordError(message);
            Console.WriteLine("ERROR: " + message);
            cts.Cancel();
        };
        Action<string>? reportStatus = message =>
        {
            if (message.StartsWith("STATUS|", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("STATUS: " + message.Substring("STATUS|".Length));
                return;
            }
            Console.WriteLine(message);
        };

        Task workerTask = Task.Run(() => worker.Run(cts.Token, reportProgress, validation, reportError, reportStatus));

        long lastOps = 0;
        while (!workerTask.IsCompleted)
        {
            Thread.Sleep(1000);
            long current = Interlocked.Read(ref totalOps);
            long delta = current - lastOps;
            lastOps = current;
            Console.WriteLine($"Throughput: {delta} ops/sec");
        }

        workerTask.GetAwaiter().GetResult();
        Console.WriteLine("Done.");
    }
}
