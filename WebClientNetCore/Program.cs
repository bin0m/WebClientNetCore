using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;

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
            runner.DoAction("Action1");

            //RetryUsingSimpleRetryHelper(servicesProvider);
            //RetryUsingAsyncRetryHelper(servicesProvider);
            RetryUsingPolly(servicesProvider);

            Console.WriteLine("End");
            // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
            NLog.LogManager.Shutdown();
            Console.ReadLine();
        }

        static void RetryUsingSimpleRetryHelper(IServiceProvider servicesProvider)
        {
            var simpleRetryHelper = servicesProvider.GetRequiredService<SimpleRetryHelper>();
          
            HttpClient httpClient = new HttpClient();
            string message = "no message";

            var maxRetryAttempts = 3;
            var pauseBetweenFailures = TimeSpan.FromSeconds(3);

            simpleRetryHelper.RetryOnException<Exception>(maxRetryAttempts, pauseBetweenFailures, () =>
            {
                message = httpClient.GetStringAsync("http://teachmetest.azurewebsites.net/api/values").Result;
            });

            Console.WriteLine($"Got message:{message}");
        }

        static void RetryUsingAsyncRetryHelper(IServiceProvider servicesProvider)
        {        
            var asyncRetryHelper = servicesProvider.GetRequiredService<AsyncRetryHelper>();

            HttpClient httpClient = new HttpClient();
            string message = "no message";

            var maxRetryAttempts = 3;
            var pauseBetweenFailures = TimeSpan.FromSeconds(3);
            var response = asyncRetryHelper.RetryOnExceptionAsync<HttpRequestException>(maxRetryAttempts, pauseBetweenFailures, async () =>
            {
                message = await httpClient.GetStringAsync("http://teachmetest.azurewebsites.net/api/values");
            });

            response.Wait();

            Console.WriteLine($"Got message:{message}");
        }

        static void RetryUsingPolly(IServiceProvider servicesProvider)
        {
            var httpClient = new HttpClient();
            string message = "no message";
            var maxRetryAttempts = 3;
            var pauseBetweenFailures = TimeSpan.FromSeconds(5);

            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(maxRetryAttempts, i => pauseBetweenFailures);
            var response =  retryPolicy.ExecuteAsync(async () =>
            {
                message = await httpClient.GetStringAsync("http://teachmetest.azurewebsites.net/api/values");
            });

            response.Wait();

            Console.WriteLine($"Got message:{message}");
        }
    }
}
