namespace FlashSale.Api.Models
{
    public class Order
    {
        public int Id { get; set; }

        public Guid TrackingId { get; set; }

        public int ProductId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
