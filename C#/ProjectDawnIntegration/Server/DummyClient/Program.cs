using System;
using System.Threading.Tasks;

namespace DummyClient
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                return MainAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DUMMY] Fatal error: {ex}");
                return 1;
            }
        }

        private static async Task<int> MainAsync(string[] args)
        {
            DummyClientOptions options;
            if (!DummyClientOptions.TryParse(args, out options))
            {
                DummyClientOptions.PrintUsage();
                return 1;
            }

            DummyClientRunner runner = new DummyClientRunner(options);
            return await runner.RunAsync();
        }
    }
}
