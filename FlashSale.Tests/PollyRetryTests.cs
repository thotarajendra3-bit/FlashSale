using Polly;
using Polly.Retry;
using Xunit;

namespace FlashSale.Tests
{
    public class PollyRetryTests
    {
        [Fact]
        public async Task Polly_Should_Retry_When_Operation_Fails()
        {
            // Arrange
            int attempts = 0;

            var retryPipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(10),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = false
                })
                .Build();

            // Act
            await retryPipeline.ExecuteAsync(
                async cancellationToken =>
                {
                    attempts++;

                    if (attempts < 3)
                    {
                        throw new Exception("Simulated database failure");
                    }

                    await Task.CompletedTask;
                });

            // Assert
            Assert.Equal(3, attempts);
        }
    }
}