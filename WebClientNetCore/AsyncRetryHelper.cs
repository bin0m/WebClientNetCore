using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace WebClientNetCore
{
    public class AsyncRetryHelper
    {
        private readonly ILogger<Runner> _logger;

        public AsyncRetryHelper(ILogger<Runner> logger)
        {
            _logger = logger;
        }

        public async Task RetryOnExceptionAsync(int times, TimeSpan delay, Func<Task> operation)
        {
            if (times <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(times));
            }

            int attempts = 0;
            do
            {
                try
                {
                    attempts++;
                    await operation();
                    break; // Sucess! Lets exit the loop!
                }
                catch (Exception ex)
                {
                    if (attempts == times)
                    {
                        throw;
                    }

                    _logger.LogWarning(ex, $"Exception caught on attempt {attempts} - will retry after delay {delay}");

                    await Task.Delay(delay);
                }

            }
            while (true);
        }
    }
}
