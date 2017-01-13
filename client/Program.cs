using CommandLine;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication
{
    public class Program
    {
        private const int _parallel = 64;

        private static readonly HttpClient _httpClient = new HttpClient();
        private const string _payload =
            @"{ ""data"": ""{'job_id':'c4bb6d130003','container_id':'ab7b85dcac72','status':'Success: process exited with code 0.'}"" }";

        private static Stopwatch _stopwatch = Stopwatch.StartNew();
        private static long _requests;

        private class Options
        {
            [Option('u', "uri", Required = true)]
            public Uri Uri { get; set; }

            [Option('m', "method", Default = HttpMethod.Get)]
            public HttpMethod Method { get; set; }

            [Option('p', "parallel", Default = 64)]
            public int Parallel { get; set; }
        }

        private enum HttpMethod
        {
            Get,
            Post
        }

        public static int Main(string[] args)
        {
            var parser = new Parser(settings => settings.CaseInsensitiveEnumValues = true);

            return parser.ParseArguments<Options>(args).MapResult(
                options => Run(options),
                _ => 1
            );
        }

        private static int Run(Options options)
        {
            Console.WriteLine($"{options.Method.ToString().ToUpperInvariant()} {options.Uri} with {options.Parallel} clients...");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            WriteResults();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            RunTest(options.Uri, options.Method, options.Parallel);

            return 0;
        }

        private static void RunTest(Uri uri, HttpMethod method, int parallel) {
            var threadObjects = new Thread[_parallel];

            for (var i=0; i < _parallel; i++) {
                var thread = new Thread(() =>
                {
                    while (true) {
                        if (method == HttpMethod.Get) {
                            using (var response = _httpClient.GetAsync(uri).Result) { }
                        }
                        else if (method == HttpMethod.Post) {
                            using (var response = _httpClient.PostAsync(uri, new StringContent(_payload)).Result) { }
                        }
                        else {
                            throw new InvalidOperationException();
                        }
                        Interlocked.Increment(ref _requests);
                    }
                });
                threadObjects[i] = thread;
                thread.Start();
            }
            
            for (var i=0; i < _parallel; i++) {
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

                WriteResult(_requests, elapsed, currentRequests, currentElapsed);
            }
        }

        private static void WriteResult(long totalRequests, TimeSpan totalElapsed, long currentRequests, TimeSpan currentElapsed)
        {
            Console.WriteLine($"{DateTime.UtcNow.ToString("o")}\tTotal Requests\t{totalRequests}" +
                $"\tCurrent RPS\t{Math.Round(currentRequests / currentElapsed.TotalSeconds)}" +
                $"\tAverage RPS\t{Math.Round(totalRequests / totalElapsed.TotalSeconds)}");
        }        
    }
}
