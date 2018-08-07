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

        public async Task RetryOnExceptionAsync<TException>(int times, TimeSpan delay, Func<Task> operation) where TException:Exception
        {
            if (times <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(times));
            }

            int attempts = 0;
            // half the delay, because it will be multiplied by 2 before actually delaying;
            delay /= 2;
            do
            {
                try
                {
                    attempts++;
                    await operation();
                    break; // Sucess! Lets exit the loop!
                }
                catch (TException ex)
                {
                    if (attempts == times)
                    {
                        throw;
                    }

                    // increase delay 2 times after failed attempt
                    delay *= 2;

                    _logger.LogWarning(ex, $"Exception caught on attempt {attempts} - will retry after delay {delay}");
                    await Task.Delay(delay);
                }

            }
            while (true);
        }
    }
}
