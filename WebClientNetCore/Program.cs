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
            // initializations
            var servicesProvider = BuildDi();
            var runner = servicesProvider.GetRequiredService<Runner>();
            var simpleRetryHelper = servicesProvider.GetRequiredService<SimpleRetryHelper>();

            runner.DoAction("Action1");

            Console.WriteLine("Start");
            HttpClient httpClient = new HttpClient();
            string message = "no message";

            var maxRetryAttempts = 3;
            var pauseBetweenFailures = TimeSpan.FromSeconds(5);
            simpleRetryHelper.RetryOnException(maxRetryAttempts, pauseBetweenFailures, () => {
                message = httpClient.GetStringAsync("http://teachmetest.azurewebsites.net/api/values").Result;
            });

            Console.WriteLine($"Got message:{message}");
            Console.ReadLine();

            // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
            NLog.LogManager.Shutdown();
        }
    }
}
