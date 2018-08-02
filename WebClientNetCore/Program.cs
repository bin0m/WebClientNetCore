using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace WebClientNetCore
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Start");
            HttpClient httpClient = new HttpClient();
            string message = "no message";

            var maxRetryAttempts = 3;
            var pauseBetweenFailures = TimeSpan.FromSeconds(5);
            SimpleRetryHelper.RetryOnException(maxRetryAttempts, pauseBetweenFailures, () => {
                message = httpClient.GetStringAsync("http://teachmetest.azurewebsites.net/api/values").Result;
            });

            Console.WriteLine($"Got message:{message}");
            Console.ReadLine();
        }
    }
}
