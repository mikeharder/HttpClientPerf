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
                var threads = new Thread[_parallel];

                for (var i = 0; i < _parallel; i++)
                {
                    var thread = new Thread(() =>
                    {
                        while (true)
                        {
                            using (var response = ExecuteRequestAsync(uri, method).Result) { }
                            Interlocked.Increment(ref _requests);
                        }
                    });
                    threads[i] = thread;
                    thread.Start();
                }

                for (var i = 0; i < _parallel; i++)
                {
                    threads[i].Join();
                }
            }
            else if (threadingMode == ThreadingMode.Task)
            {
                var tasks = new Task[_parallel];
                for (var i=0; i < _parallel; i++)
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
                await ExecuteRequestAsync(uri, method);
                Interlocked.Increment(ref _requests);
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
