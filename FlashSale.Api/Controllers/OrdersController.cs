using FlashSale.Api.DTOs;
using FlashSale.Api.Queue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FlashSale.Api.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : Controller
    {
        private readonly OrderQueue _orderQueue;
        public OrdersController(OrderQueue orderQueue)
        {
            _orderQueue = orderQueue;
        }
        [HttpPost]
        [EnableRateLimiting("OrderRateLimit")]
        public async Task<IActionResult> CreateOrder(
            [FromBody] CreateOrderRequest request,
            [FromHeader(Name = "X-User-Id")] string userId,
            CancellationToken cancellationToken)
        {
            // Validate request
            if (request.ProductId <= 0)
            {
                return BadRequest(new
                {
                    message = "ProductId must be greater than 0."
                });
            }

            if (request.Quantity <= 0)
            {
                return BadRequest(new
                {
                    message = "Quantity must be greater than 0."
                });
            }

            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return BadRequest(new
                {
                    message = "UserId is required."
                });
            }

            // Generate tracking ID
            var trackingId = Guid.NewGuid();

            // Create queue message
            var orderMessage = new OrderMessage
            {
                TrackingId = trackingId,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                UserId = userId
            };

            // Put order into Channel
            await _orderQueue.EnqueueAsync(
                orderMessage,
                cancellationToken);

            // Immediately return 202 Accepted
            return Accepted(new
            {
                trackingId = trackingId,
                status = "Pending"
            });
        }
    }
}
