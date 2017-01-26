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
        private static HttpClient[] _httpClients;
        private static long _httpClientCounter = 0;

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

            [Option('c', "clients", Default = 1)]
            public int Clients { get; set; }

            [Option('e', "expectContinue")]
            public bool? ExpectContinue { get; set; }

            [Option('r', "requests", Default = long.MaxValue)]
            public long Requests { get; set; }

            [Option('s', "clientSelectionMode", Default = ClientSelectionMode.Task)]
            public ClientSelectionMode ClientSelectionMode { get; set; }
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

        private enum ClientSelectionMode
        {
            Task,
            Request
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
            Console.WriteLine(
                $"{options.Method.ToString().ToUpperInvariant()} {options.Uri} with " +
                $"{options.Parallel} {options.ThreadingMode.ToString().ToLowerInvariant()}(s), " +
                $"{options.Clients} client(s), " +
                $"ClientSelectionMode={options.ClientSelectionMode.ToString()}, "
                $"and ExpectContinue={options.ExpectContinue?.ToString() ?? "null"}" +
                "...");

            var writeResultsTask = WriteResults(options.Requests);

            RunTest(options.Uri, options.Method, options.Parallel, options.ThreadingMode, options.Clients, options.ClientSelectionMode,
                options.ExpectContinue, options.Requests);

            writeResultsTask.Wait();

            return 0;
        }

        private static void RunTest(Uri uri, HttpMethod method, int parallel, ThreadingMode threadingMode, int clients,
            ClientSelectionMode clientSelectionMode, bool? expectContinue, long maxRequests)
        {
            _httpClients = new HttpClient[clients];
            for (int i = 0; i < clients; i++)
            {
                _httpClients[i] = new HttpClient();
            }

            if (threadingMode == ThreadingMode.Thread)
            {
                var threads = new Thread[parallel];

                for (var i = 0; i < parallel; i++)
                {
                    HttpClient httpClient = null;
                    if (clientSelectionMode == ClientSelectionMode.Task)
                    {
                        httpClient =  _httpClients[i % clients];
                    }

                    var thread = new Thread(() =>
                    {
                        while (Interlocked.Increment(ref _requests) <= maxRequests)
                        {
                            var start = _stopwatch.ElapsedTicks;
                            using (var response = ExecuteRequestAsync(httpClient, uri, method, expectContinue).Result) { }
                            var end = _stopwatch.ElapsedTicks;

                            Interlocked.Add(ref _ticks, end - start);
                        }
                        Interlocked.Decrement(ref _requests);
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
                for (var i = 0; i < parallel; i++)
                {
                    HttpClient httpClient = null;
                    if (clientSelectionMode == ClientSelectionMode.Task)
                    {
                        httpClient = _httpClients[i % clients];
                    }
                    var task = ExecuteRequestsAsync(httpClient, uri, method, expectContinue, maxRequests);
                    tasks[i] = task;
                }

                Task.WaitAll(tasks);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static HttpClient NextHttpClient()
        {
            return _httpClients[Interlocked.Increment(ref _httpClientCounter) % _httpClients.Length];
        }

        private static Task<HttpResponseMessage> ExecuteRequestAsync(HttpClient httpClient, Uri uri, HttpMethod method, bool? expectContinue)
        {
            if (httpClient == null)
            {
                httpClient = NextHttpClient();
            }

            if (method == HttpMethod.Get)
            {
                return httpClient.GetAsync(uri);
            }
            else if (method == HttpMethod.Post)
            {
                var m = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, uri);
                m.Content = new StringContent(_payload);
                m.Headers.ExpectContinue = expectContinue;
                return httpClient.SendAsync(m);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static async Task ExecuteRequestsAsync(HttpClient httpClient, Uri uri, HttpMethod method, bool? expectContinue, long maxRequests)
        {
            while (Interlocked.Increment(ref _requests) <= maxRequests)
            {
                var start = _stopwatch.ElapsedTicks;
                await ExecuteRequestAsync(httpClient, uri, method, expectContinue);
                var end = _stopwatch.ElapsedTicks;

                Interlocked.Add(ref _ticks, end - start);
            }
            Interlocked.Decrement(ref _requests);
        }

        private static async Task WriteResults(long maxRequests)
        {
            var lastRequests = (long)0;
            var lastTicks = (long)0;
            var lastElapsed = TimeSpan.Zero;

            do
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
            } while (Interlocked.Read(ref _requests) < maxRequests);
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
