namespace FlashSale.Api.DTOs
{
    public class CreateOrderRequest
    {
        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public string UserId { get; set; } = string.Empty;
    }
}
