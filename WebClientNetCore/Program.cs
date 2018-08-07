using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace WebClientNetCore
{
    class Program
    {
        private static IServiceProvider BuildDi()
        {
            var services = new ServiceCollection();

            services.AddTransient<Runner>();
            services.AddSingleton<SimpleRetryHelper>();
            services.AddSingleton<AsyncRetryHelper>();

            services.AddSingleton<ILoggerFactory, LoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddLogging((builder) => builder.SetMinimumLevel(LogLevel.Trace));

            var serviceProvider = services.BuildServiceProvider();

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            //configure NLog
            loggerFactory.AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true });
            NLog.LogManager.LoadConfiguration("nlog.config");

            return serviceProvider;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Start");

            // initializations
            var servicesProvider = BuildDi();
            var runner = servicesProvider.GetRequiredService<Runner>();
            var simpleRetryHelper = servicesProvider.GetRequiredService<SimpleRetryHelper>();
            var asyncRetryHelper = servicesProvider.GetRequiredService<AsyncRetryHelper>();

            runner.DoAction("Action1");
                        
            HttpClient httpClient = new HttpClient();
            string message = "no message";

            var maxRetryAttempts = 3;
            var pauseBetweenFailures = TimeSpan.FromSeconds(3);
            //simpleRetryHelper.RetryOnException<HttpRequestException>(maxRetryAttempts, pauseBetweenFailures, () => {
            //    message = httpClient.GetStringAsync("http://teachmetest.azurewebsites.net/api/values").Result;
            //});
            var response = asyncRetryHelper.RetryOnExceptionAsync<HttpRequestException>(maxRetryAttempts, pauseBetweenFailures, async () =>
            {
                message = await httpClient.GetStringAsync("http://teachmetest.azurewebsites.net/api/values");
            });

            response.Wait();

            Console.WriteLine($"Got message:{message}");

            // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
            NLog.LogManager.Shutdown();
            Console.ReadLine();


        }
    }
}
