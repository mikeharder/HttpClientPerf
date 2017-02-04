using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ConsoleApplication
{
    public class Startup
    {
        public static void Main(string[] args)
        {
            var hostBuilder = new WebHostBuilder()
                .UseUrls("http://+:8080", "https://+:8081")
                .UseKestrel(options =>
                {
                    options.UseHttps("testCert.pfx", "testPassword");
                })
                .UseStartup<Startup>();
            
            var host = hostBuilder.Build();
            host.Run();            
        }

        public void Configure(IApplicationBuilder app)
        {
            // app.UseDeveloperExceptionPage();

            app.Run(context =>
            {
                if (context.Request.ContentLength > 0) {
                    return context.Request.Body.CopyToAsync(context.Response.Body);
                }
                else {
                    return context.Response.WriteAsync("Hello from ASP.NET Core!");
                }
            });
        }
    }
}
