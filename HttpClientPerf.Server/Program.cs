using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace HttpClientPerf.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
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
                        return context.Response.WriteAsync("Hello from ASP.NET Core!");
                    }
                }))
                .Build()
                .Run();
        }
    }
}
