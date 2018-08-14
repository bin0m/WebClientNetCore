using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.Timeout;
using System.Linq;
using System.Collections.Generic;

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
            RetryUsingAsyncRetryHelper(servicesProvider);
            //RetryUsingPolly();
            //Task.Factory.StartNew(async () => await RetryAndTimeoutUsingPolly());

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

        static void RetryUsingPolly()
        {
            var httpClient = new HttpClient();
            string message = "no message";
            var maxRetryAttempts = 3;
            var pauseBetweenFailures = TimeSpan.FromSeconds(5);

            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(maxRetryAttempts, i => pauseBetweenFailures);
            var response = retryPolicy.ExecuteAsync(async () =>
           {
               message = await httpClient.GetStringAsync("http://teachmetest.azurewebsites.net/api/values");
           });

            response.Wait();

            Console.WriteLine($"Got message:{message}");
        }

        static async Task RetryAndTimeoutUsingPolly()
        {
            var httpClient = new HttpClient();
            string message = "no message";
            var maxRetryAttempts = 3;
            var pauseBetweenFailures = TimeSpan.FromSeconds(5);
            var timeoutInSec = 20;

            //Retry Policy
            var retryPolicy = Policy
                .Handle<Exception>()
                //.Or<AnyOtherException>()
                .WaitAndRetryAsync(
                    maxRetryAttempts,
                    i => pauseBetweenFailures,
                    (exception, timeSpan, retryCount, context) => ManageRetryException(exception, timeSpan, retryCount, context));

            //TimeOut Policy
            var timeOutPolicy = Policy
                .TimeoutAsync(
                    timeoutInSec,
                    TimeoutStrategy.Pessimistic,
                    (context, timeSpan, task) => ManageTimeoutException(context, timeSpan, task));

            //Combine the two (or more) policies
            var policyWrap = Policy.WrapAsync(retryPolicy, timeOutPolicy);

            //Execute the transient task(s)
            await policyWrap.ExecuteAsync(async (ct) =>
            {
                Console.WriteLine("\r\nExecuting RetryAndTimeoutUsingPolly task...");
                message = await httpClient.GetStringAsync("http://teachmetest.azurewebsites.net/api/values");
            }, new Dictionary<string, object>() { { "ExecuteOperation", "Operation description..." } });

            Console.WriteLine($"Got message:{message}");

            return;
        }

        private static void ManageRetryException(Exception exception, TimeSpan timeSpan, int retryCount, Context context)
        {
            var action = context != null ? context.First().Key : "unknown method";
            var actionDescription = context != null ? context.First().Value : "unknown description";
            var msg = $"Retry n°{retryCount} of {action} ({actionDescription}) : {exception.Message}";
            Console.WriteLine(msg);
        }

        private static Task ManageTimeoutException(Context context, TimeSpan timeSpan, Task task)
        {
            var action = context != null ? context.First().Key : "unknown method";
            var actionDescription = context != null ? context.First().Value : "unknown description";

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var msg = $"Running {action} ({actionDescription}) but the execution timed out after {timeSpan.TotalSeconds} seconds, eventually terminated with: {t.Exception}.";
                    Console.WriteLine(msg);
                }
                else if (t.IsCanceled)
                {
                    var msg = $"Running {action} ({actionDescription}) but the execution timed out after {timeSpan.TotalSeconds} seconds, task cancelled.";
                    Console.WriteLine(msg);
                }
            });

            return task;
        }
    }
}
