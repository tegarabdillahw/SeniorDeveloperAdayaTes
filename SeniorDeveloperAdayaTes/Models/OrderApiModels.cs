using System.ComponentModel.DataAnnotations;
using static SeniorDeveloperAdayaTes.Models.OrderModels;

namespace SeniorDeveloperAdayaTes.Models
{
    public class CreateOrderRequest
    {
        [Range(1, long.MaxValue)]
        public long CustomerId { get; set; }

        [Required, MaxLength(500)]
        public string ShippingAddress { get; set; } = string.Empty;

        [Required, MinLength(1)]
        public List<CreateOrderItemRequest> Items { get; set; } = new();
    }

    public class CreateOrderItemRequest
    {
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }

    
    public class UpdateOrderStatusRequest
    {
        [Required]
        public OrderStatus? Status { get; set; }
    }

   
    public class OrderResponse
    {
        public Guid Id { get; set; }
        public long CustomerId { get; set; }
        public string ShippingAddress { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItemResponse> Items { get; set; } = new();
    }

    public class OrderItemResponse
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class PagedResponse<T>
    {
        public List<T> Data { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalData { get; set; }
        public int TotalPages { get; set; }
    }


    public class ApiException : Exception
    {
        public int StatusCode { get; }
        public string Code { get; }
        public object? Details { get; }

        public ApiException(int statusCode, string code, string message, object? details = null)
            : base(message)
        {
            StatusCode = statusCode;
            Code = code;
            Details = details;
        }
    }
}
