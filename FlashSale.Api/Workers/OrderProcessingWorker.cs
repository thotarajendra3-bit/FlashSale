using FlashSale.Api.Data;
using FlashSale.Api.Models;
using FlashSale.Api.Queue;
using Polly;    
using Microsoft.EntityFrameworkCore;
using Polly.Retry;

namespace FlashSale.Api.Workers
{
    public class OrderProcessingWorker : BackgroundService
    {
        private readonly ResiliencePipeline _retryPipeline;
        private readonly OrderQueue _orderQueue;
        private readonly IServiceScopeFactory _scopeFactory;

        public OrderProcessingWorker(
           OrderQueue orderQueue,
           IServiceScopeFactory scopeFactory)
        {
            _orderQueue = orderQueue;
            _scopeFactory = scopeFactory;

            _retryPipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                })
                .Build();
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            await foreach (var order in _orderQueue.ReadAllAsync(stoppingToken))
            {
                await _retryPipeline.ExecuteAsync(
                async cancellationToken =>
            {
                await ProcessOrderAsync(order, cancellationToken);
            },
                stoppingToken);
            }
        }

        private async Task ProcessOrderAsync(
            DTOs.OrderMessage order,
            CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    cancellationToken);

            try
            {
                // Lock the product row until this transaction completes
                var product = await db.Products
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM "Products"
                        WHERE "Id" = {order.ProductId}
                        FOR UPDATE
                        """)
                    .SingleOrDefaultAsync(cancellationToken);

                if (product == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return;
                }

                string status;

                if (product.Stock >= order.Quantity)
                {
                    product.Stock -= order.Quantity;
                    status = "Success";
                }
                else
                {
                    status = "OutOfStock";
                }

                db.Orders.Add(new Order
                {
                    TrackingId = order.TrackingId,
                    ProductId = order.ProductId,
                    UserId = order.UserId,
                    Quantity = order.Quantity,
                    Status = status,
                    CreatedAt = DateTime.UtcNow
                });

                await db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}