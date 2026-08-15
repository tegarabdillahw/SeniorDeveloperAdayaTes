using System.ComponentModel.DataAnnotations;

namespace SeniorDeveloperAdayaTes.Models
{
    public class OrderModels
    {

        public enum OrderStatus
        {
            Pending = 1,
            Confirmed = 2,
            Shipped = 3,
            Delivered = 4,
            Cancelled = 5
        }

       
        public class Product
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int StockQuantity { get; set; }
            public decimal Price { get; set; }
        }
        public class Order
        {
            public Guid Id { get; set; }
            public long CustomerId { get; set; }
            public string ShippingAddress { get; set; } = string.Empty;
            public OrderStatus Status { get; set; }
            public DateTime CreatedAtUtc { get; set; }
            public DateTime UpdatedAtUtc { get; set; }

            [Timestamp]
            public byte[] RowVersion { get; set; } = Array.Empty<byte>();

            public List<OrderItem> Items { get; set; } = new();
        }

      
        public class OrderItem
        {
            public long Id { get; set; }
            public Guid OrderId { get; set; }
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }

            public Order Order { get; set; } = null!;
        }

      
        public class IdempotencyKey
        {
            public string Key { get; set; } = string.Empty;
            public Guid? OrderId { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }

    }
}
