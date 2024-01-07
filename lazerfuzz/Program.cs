using osu.Framework;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Platform.Windows;
using osu.Framework.Testing;
using SharpFuzz;
using System.Runtime;
using System.Text;

#nullable disable

namespace lazerfuzz;

internal partial class Program
{
    static private GameHost host;
    static private Task runTask;
    static private FuzzerGame game;

    static private void checkForErrors()
    {
        if (host.ExecutionState == ExecutionState.Stopping)
            runTask.WaitSafely();

        if (runTask.Exception != null)
        {
            Console.WriteLine(runTask.Exception);
            throw runTask.Exception;
        }
    }

    static unsafe void Main(string[] args)
    {
        if (args.Length > 0)
        {
            Console.WriteLine("Running in GUI repro mode");
            host = Host.GetSuitableDesktopHost("fuzzer");
        }
        else
        {
            Console.WriteLine("Running in headless fuzz mode");
            host = new TestRunHeadlessGameHost();
        }
        game = new FuzzerGame();
        runTask = Task.Factory.StartNew(() => host.Run(game), TaskCreationOptions.LongRunning);

        while (!game.IsLoaded)
        {
            checkForErrors();
            Thread.Sleep(10);
        }

        if (args.Length > 0)
        {
            game.Initialize();
            if (args[0] == "-")
                game.Run(Console.OpenStandardInput(), true);
            else
                game.Run(File.OpenRead(args[0]), true);
            checkForErrors();
            return;
        }

        for (int i = 0; i < warmupInputs.Length; i++)
        {
            Console.WriteLine($"🫠 Running warmup {i + 1}/{warmupInputs.Length}");
            game.Initialize();
            game.Run(new MemoryStream(warmupInputs[i]));
            checkForErrors();
        }

        Logger.Level = LogLevel.Verbose;
        game.Initialize();
        GCSettings.LatencyMode = GCLatencyMode.Batch;
        GC.Collect(0, GCCollectionMode.Forced, true, true);
        game.Run(null);
    }
}
