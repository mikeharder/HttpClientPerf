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
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string _payload =
            @"{ ""data"": ""{'job_id':'c4bb6d130003','container_id':'ab7b85dcac72','status':'Success: process exited with code 0.'}"" }";

        private static Stopwatch _stopwatch = Stopwatch.StartNew();

        private static long _requests;
        private static long _ticks;

        private class Options
        {
            [Option('u', "uri", Required = true)]
            public Uri Uri { get; set; }

            [Option('m', "method", Default = HttpMethod.Get)]
            public HttpMethod Method { get; set; }

            [Option('p', "parallel", Default = 512)]
            public int Parallel { get; set; }

            [Option('t', "threadingMode", Default = ThreadingMode.Task)]
            public ThreadingMode ThreadingMode { get; set; }
        }

        private enum HttpMethod
        {
            Get,
            Post
        }

        private enum ThreadingMode
        {
            Task,
            Thread
        }

        public static int Main(string[] args)
        {
            var parser = new Parser(settings =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = Console.Out;
            });

            return parser.ParseArguments<Options>(args).MapResult(
                options => Run(options),
                _ => 1
            );
        }

        private static int Run(Options options)
        {
            Console.WriteLine($"{options.Method.ToString().ToUpperInvariant()} {options.Uri} " +
                $"with {options.Parallel} {options.ThreadingMode.ToString().ToLowerInvariant()}s ...");

            var writeResultsTask = WriteResults();

            RunTest(options.Uri, options.Method, options.Parallel, options.ThreadingMode);

            writeResultsTask.Wait();

            return 0;
        }

        private static void RunTest(Uri uri, HttpMethod method, int parallel, ThreadingMode threadingMode) {
            if (threadingMode == ThreadingMode.Thread)
            {
                var threads = new Thread[parallel];

                for (var i = 0; i < parallel; i++)
                {
                    var thread = new Thread(() =>
                    {
                        while (true)
                        {
                            var start = _stopwatch.ElapsedTicks;
                            using (var response = ExecuteRequestAsync(uri, method).Result) { }
                            var end = _stopwatch.ElapsedTicks;

                            Interlocked.Increment(ref _requests);
                            Interlocked.Add(ref _ticks, end - start);
                        }
                    });
                    threads[i] = thread;
                    thread.Start();
                }

                for (var i = 0; i < parallel; i++)
                {
                    threads[i].Join();
                }
            }
            else if (threadingMode == ThreadingMode.Task)
            {
                var tasks = new Task[parallel];
                for (var i=0; i < parallel; i++)
                {
                    var task = ExecuteRequestsAsync(uri, method);
                    tasks[i] = task;
                }

                Task.WaitAll(tasks);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static Task<HttpResponseMessage> ExecuteRequestAsync(Uri uri, HttpMethod method)
        {
            if (method == HttpMethod.Get)
            {
                return _httpClient.GetAsync(uri);
            }
            else if (method == HttpMethod.Post)
            {
                return _httpClient.PostAsync(uri, new StringContent(_payload));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static async Task ExecuteRequestsAsync(Uri uri, HttpMethod method)
        {
            while (true)
            {
                var start = _stopwatch.ElapsedTicks;
                await ExecuteRequestAsync(uri, method);
                var end = _stopwatch.ElapsedTicks;

                Interlocked.Increment(ref _requests);
                Interlocked.Add(ref _ticks, end - start);
            }
        }

        private static async Task WriteResults()
        {
            var lastRequests = (long)0;
            var lastTicks = (long)0;
            var lastElapsed = TimeSpan.Zero;

            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));

                var requests = _requests;
                var currentRequests = requests - lastRequests;
                lastRequests = requests;

                var ticks = _ticks;
                var currentTicks = ticks - lastTicks;
                lastTicks = ticks;

                var elapsed = _stopwatch.Elapsed;
                var currentElapsed = elapsed - lastElapsed;
                lastElapsed = elapsed;

                WriteResult(requests, ticks, elapsed, currentRequests, currentTicks, currentElapsed);
            }
        }

        private static void WriteResult(long totalRequests, long totalTicks, TimeSpan totalElapsed,
            long currentRequests, long currentTicks, TimeSpan currentElapsed)
        {
            var totalMs = ((double)totalTicks / Stopwatch.Frequency) * 1000;
            var currentMs = ((double)currentTicks / Stopwatch.Frequency) * 1000;

            Console.WriteLine(
                $"{DateTime.UtcNow.ToString("o")}\tTot Req\t{totalRequests}" +
                $"\tCur RPS\t{Math.Round(currentRequests / currentElapsed.TotalSeconds)}" +
                $"\tCur Lat\t{Math.Round(currentMs / currentRequests, 2)}ms" +
                $"\tAvg RPS\t{Math.Round(totalRequests / totalElapsed.TotalSeconds)}" +
                $"\tAvg Lat\t{Math.Round(totalMs / totalRequests, 2)}ms"
            );
        }        
    }
}
