using System.Net;
using System.Net.Http.Json;
using FlashSale.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlashSale.Tests
{
    public class FlashSaleConcurrencyTests
    {
        [Fact]
        public async Task Fifty_Parallel_Orders_Should_Not_Oversell()
        {
            // Start the API
            await using var factory =
                new WebApplicationFactory<Program>();

            using var client = factory.CreateClient();

            // Reset product stock and remove old test orders
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

                // Remove old orders for Product 1
                var oldOrders = await db.Orders
                    .Where(o => o.ProductId == 1)
                    .ToListAsync();

                db.Orders.RemoveRange(oldOrders);

                // Reset stock to 5
                var product = await db.Products
                    .FirstAsync(p => p.Id == 1);

                product.Stock = 5;

                await db.SaveChangesAsync();
            }
            // Send 50 orders in parallel
            var tasks = Enumerable.Range(1, 50)
                .Select(async i =>
                {
                    var request = new
                    {
                        productId = 1,
                        quantity = 1,
                        userId = $"test-user-{i}"
                    };

                    using var requestMessage =
                        new HttpRequestMessage(
                            HttpMethod.Post,
                            "/api/orders");

                    requestMessage.Headers.Add(
                        "X-User-Id",
                        $"test-user-{i}");

                    requestMessage.Content =
                        JsonContent.Create(request);

                    return await client.SendAsync(requestMessage);
                });

            var responses = await Task.WhenAll(tasks);

            // All 50 should be accepted by the API
            var acceptedCount = responses.Count(
                response => response.StatusCode == HttpStatusCode.Accepted);

            Assert.Equal(50, acceptedCount);

            // Wait for background worker to process all orders
            using var dbScope = factory.Services.CreateScope();

            var dbContext = dbScope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var processed = 0;

            for (int i = 0; i < 50; i++)
            {
                processed = await dbContext.Orders
                    .CountAsync(o => o.ProductId == 1);

                if (processed >= 50)
                    break;

                await Task.Delay(100);
            }

            // Check exactly 5 successful orders
            var successfulOrders = await dbContext.Orders
                .CountAsync(o =>
                    o.ProductId == 1 &&
                    o.Status == "Success");

            // Check remaining orders
            var outOfStockOrders = await dbContext.Orders
                .CountAsync(o =>
                    o.ProductId == 1 &&
                    o.Status == "OutOfStock");

            // Check final stock
            var finalStock = await dbContext.Products
                .Where(p => p.Id == 1)
                .Select(p => p.Stock)
                .FirstAsync();

            Assert.Equal(50, processed);
            Assert.Equal(5, successfulOrders);
            Assert.Equal(45, outOfStockOrders);
            Assert.Equal(0, finalStock);
        }
    }
}