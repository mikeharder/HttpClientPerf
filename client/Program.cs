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
        private static int[] _queuedRequests;
        private static int _minQueue;
        private static int _maxQueue;

        private const string _payload =
            @"{ ""data"": ""{'job_id':'c4bb6d130003','container_id':'ab7b85dcac72','status':'Success: process exited with code 0.'}"" }";

        private static Stopwatch _stopwatch = Stopwatch.StartNew();

        private static long _requests;
        private static long _ticks;

        private static Options _options;

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

            [Option('s', "clientSelectionMode", Default = ClientSelectionMode.TaskRoundRobin)]
            public ClientSelectionMode ClientSelectionMode { get; set; }

            [Option('o', "clientSelectionTolerance", Default = 0)]
            public int ClientSelectionTolerance { get; set; }

            [Option('v', "verbose", Default = false)]
            public bool Verbose { get; set; }
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
            TaskRoundRobin,
            TaskRandom,
            RequestRoundRobin,
            RequestRandom,
            RequestShortestQueue,
            RequestRandomNotLongestQueue,
            RequestRandomTolerance
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
            _options = options;

            Console.WriteLine(
                $"{options.Method.ToString().ToUpperInvariant()} {options.Uri} with " +
                $"{options.Parallel} {options.ThreadingMode.ToString().ToLowerInvariant()}(s), " +
                $"{options.Clients} client(s), " +
                $"ClientSelectionMode={options.ClientSelectionMode.ToString()}, " +
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
            _queuedRequests = new int[clients];
            _minQueue = (parallel / clients) - _options.ClientSelectionTolerance;
            _maxQueue = (parallel / clients) + _options.ClientSelectionTolerance;

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
                    int clientId = -1;
                    if (clientSelectionMode == ClientSelectionMode.TaskRoundRobin)
                    {
                        clientId = i % _httpClients.Length;
                    }
                    else if (clientSelectionMode == ClientSelectionMode.TaskRandom)
                    {
                        clientId = ConcurrentRandom.Next() % _httpClients.Length;
                    }

                    var thread = new Thread(() =>
                    {
                        long requestId;
                        while ((requestId = Interlocked.Increment(ref _requests)) <= maxRequests)
                        {
                            var start = _stopwatch.ElapsedTicks;
                            using (var response = ExecuteRequestAsync(requestId, clientId, uri, method, expectContinue, clientSelectionMode).Result) { }
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
                    int clientId = -1;
                    if (clientSelectionMode == ClientSelectionMode.TaskRoundRobin)
                    {
                        clientId = i % _httpClients.Length;
                    }
                    else if (clientSelectionMode == ClientSelectionMode.TaskRandom)
                    {
                        clientId = ConcurrentRandom.Next() % _httpClients.Length;
                    }
                    var task = ExecuteRequestsAsync(clientId, uri, method, expectContinue, maxRequests, clientSelectionMode);
                    tasks[i] = task;
                }

                Task.WaitAll(tasks);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static int NextClientId()
        {
            return (int)(Interlocked.Increment(ref _httpClientCounter) % _httpClients.Length);
        }

        private static int ShortestQueue()
        {
            var shortestQueue = 0;
            var shortestQueueLength = _queuedRequests[0];

            for (var i = 1; i < _httpClients.Length; i++)
            {
                var queueLength = _queuedRequests[i];
                if (queueLength < shortestQueueLength)
                {
                    shortestQueue = i;
                    shortestQueueLength = queueLength;
                }
            }

            return shortestQueue;
        }

        private static int LongestQueue()
        {
            var longestQueue = 0;
            var longestQueueLength = _queuedRequests[0];

            for (var i = 1; i < _httpClients.Length; i++)
            {
                var queueLength = _queuedRequests[i];
                if (queueLength > longestQueueLength)
                {
                    longestQueue = i;
                    longestQueueLength = queueLength;
                }
            }

            return longestQueue;
        }

        private static async Task<HttpResponseMessage> ExecuteRequestAsync(long requestId, int clientId, Uri uri, HttpMethod method, bool? expectContinue,
            ClientSelectionMode clientSelectionMode)
        {
            if (clientSelectionMode == ClientSelectionMode.RequestRoundRobin)
            {
                clientId = NextClientId();
            }
            else if (clientSelectionMode == ClientSelectionMode.RequestRandom)
            {
                clientId = ConcurrentRandom.Next() % _httpClients.Length;
            }
            else if (clientSelectionMode == ClientSelectionMode.RequestShortestQueue)
            {
                clientId = ShortestQueue();
            }
            else if (clientSelectionMode == ClientSelectionMode.RequestRandomNotLongestQueue)
            {
                var longestQueue = LongestQueue();

                int random;
                do
                {
                    random = ConcurrentRandom.Next() % _httpClients.Length;
                }
                while (random == longestQueue);

                clientId = random;
            }
            else if (clientSelectionMode == ClientSelectionMode.RequestRandomTolerance)
            {
                // If any queue is below the minimum, select it.  Else, select a random queue below the maximum.

                var shortestQueue = ShortestQueue();                
                if (_queuedRequests[shortestQueue] < _minQueue)
                {
                    clientId = shortestQueue;
                }
                else
                {
                    int random;
                    do
                    {
                        random = ConcurrentRandom.Next() % _httpClients.Length;
                    }
                    while (_queuedRequests[random] >= _maxQueue);

                    clientId = random;
                }
            }

            var managedThreadIdBefore = _options.Verbose ? Thread.CurrentThread.ManagedThreadId : -1;

            var httpClient = _httpClients[clientId];
            HttpResponseMessage response;

            Interlocked.Increment(ref _queuedRequests[clientId]);

            if (method == HttpMethod.Get)
            {
                response = await httpClient.GetAsync(uri);
            }
            else if (method == HttpMethod.Post)
            {
                using (var m = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, uri))
                using (var content = new StringContent(_payload))
                {
                    m.Content = content;
                    m.Headers.ExpectContinue = expectContinue;
                    response = await httpClient.SendAsync(m);
                }
            }
            else
            {
                throw new InvalidOperationException();
            }

            Interlocked.Decrement(ref _queuedRequests[clientId]);

            if (_options.Verbose)
            {
                Console.Error.WriteLine($"RequestId:{requestId}, ClientId:{clientId}, " +
                    $"ThreadIdBefore: {managedThreadIdBefore} ThreadIdAfter:{Thread.CurrentThread.ManagedThreadId}");
            }

            return response;
        }

        private static async Task ExecuteRequestsAsync(int clientId, Uri uri, HttpMethod method, bool? expectContinue, long maxRequests,
            ClientSelectionMode clientSelectionMode)
        {
            long requestId;
            while ((requestId = Interlocked.Increment(ref _requests)) <= maxRequests)
            {
                var start = _stopwatch.ElapsedTicks;
                using (var response = await ExecuteRequestAsync(requestId, clientId, uri, method, expectContinue, clientSelectionMode))
                {
                }
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
            }
            while (Interlocked.Read(ref _requests) < maxRequests);
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
                $"\tAvg Lat\t{Math.Round(totalMs / totalRequests, 2)}ms" +
                $"\tReq\t{String.Join(" ", _queuedRequests)}"
            );
        }

        public static class ConcurrentRandom
        {
            private static Random _global = new Random();

            [ThreadStatic]
            private static Random _local;

            public static int Next()
            {
                Random inst = _local;
                if (inst == null)
                {
                    int seed;
                    lock (_global) seed = _global.Next();
                    _local = inst = new Random(seed);
                }
                return inst.Next();
            }
        }
    }
}
