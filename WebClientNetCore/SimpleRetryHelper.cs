using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace WebClientNetCore
{
    public class SimpleRetryHelper
    {
        private readonly ILogger<Runner> _logger;

        public SimpleRetryHelper(ILogger<Runner> logger)
        {
            _logger = logger;
        }
        public void RetryOnException<TException>(int times, TimeSpan delay, Action operation) where TException : Exception
        {
            var attempts = 0;
            do
            {
                try
                {
                    attempts++;
                    operation();
                    break; // Sucess! Lets exit the loop!
                }
                catch (TException ex)
                {
                    if (attempts == times)
                        throw;

                    _logger.LogWarning(ex, $"Exception caught on attempt {attempts} - will retry after delay {delay}");

                    Task.Delay(delay).Wait();
                }
            } while (true);
        }
    }
}
