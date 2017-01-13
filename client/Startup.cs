using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication
{
    public class Startup
    {
        private const int _threads = 64;

        private static readonly HttpClient _httpClient = new HttpClient();

        private static Stopwatch _stopwatch = Stopwatch.StartNew();
        private static long _requests;

        public static void Main(string[] args)
        {
            var url = args[0];

            Console.WriteLine($"Requesting {url} with {_threads} threads...");
            
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            WriteResults();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            RunTest(args[0]);
        }

        private static void RunTest(string url) {
            var threadObjects = new Thread[_threads];

            for (var i=0; i < _threads; i++) {
                var thread = new Thread(() =>
                {
                    while (true) {
                        using (var response = _httpClient.GetAsync(url).Result) { }
                        Interlocked.Increment(ref _requests);
                    }
                });
                threadObjects[i] = thread;
                thread.Start();
            }
            
            for (var i=0; i < _threads; i++) {
                threadObjects[i].Join();
            }
        }

        private static async Task WriteResults()
        {
            var lastRequests = (long)0;
            var lastElapsed = TimeSpan.Zero;

            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));

                var currentRequests = _requests - lastRequests;
                lastRequests = _requests;

                var elapsed = _stopwatch.Elapsed;
                var currentElapsed = elapsed - lastElapsed;
                lastElapsed = elapsed;

                WriteResult(_requests, currentRequests, currentElapsed);
            }
        }

        private static void WriteResult(long totalRequests, long currentRequests, TimeSpan elapsed)
        {
            Console.WriteLine($"{DateTime.UtcNow.ToString("o")}\tRequests\t{totalRequests}\tRPS\t{Math.Round(currentRequests / elapsed.TotalSeconds)}");
        }        
    }
}
