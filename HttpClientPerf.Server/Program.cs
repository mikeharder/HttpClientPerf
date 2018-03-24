using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace HttpClientPerf.Server
{
    public class Program
    {
        private static readonly byte[] _helloWorldPayload = Encoding.UTF8.GetBytes("Hello, World!");
        private static readonly int _helloWorldPayloadLength = _helloWorldPayload.Length;

        public static void Main(string[] args)
        {
            PrintVersions();

            new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Any, 8080);
                    options.Listen(IPAddress.Any, 8081, listenOptions =>
                    {
                        listenOptions.UseHttps("testCert.pfx", "testPassword");
                    });
                })
                .Configure(app => app.Run(context =>
                {
                    if (context.Request.ContentLength > 0)
                    {
                        return context.Request.Body.CopyToAsync(context.Response.Body);
                    }
                    else
                    {
                        var response = context.Response;
                        response.StatusCode = 200;
                        response.ContentType = "text/plain";
                        response.ContentLength = _helloWorldPayloadLength;
                        return response.Body.WriteAsync(_helloWorldPayload, 0, _helloWorldPayloadLength);
                    }
                }))
                .Build()
                .Run();
        }

        private static void PrintVersions()
        {
#if NETCOREAPP2_1
            Console.WriteLine("TargetFramework: netcoreapp2.1");
#elif NETCOREAPP2_0
            Console.WriteLine("TargetFramework: netcoreapp2.0");
#else
#error Invalid TFM
#endif

            var microsoftNetCoreAppVersion = Path.GetDirectoryName(typeof(string).Assembly.Location).Split(Path.DirectorySeparatorChar).Last();
            Console.WriteLine($"Microsoft.NETCore.App: {microsoftNetCoreAppVersion}");

            var kestrelVersion = FileVersionInfo.GetVersionInfo(
                typeof(Microsoft.AspNetCore.Hosting.WebHostBuilderKestrelExtensions).Assembly.Location).ProductVersion;
            Console.WriteLine($"Kestrel: {kestrelVersion}");

            Console.WriteLine();
        }
    }
}
