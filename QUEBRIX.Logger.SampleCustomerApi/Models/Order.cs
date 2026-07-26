namespace QUEBRIX.Logger.SampleCustomerApi.Models;

/// <summary>
/// Sample order model for demonstration purposes.
/// </summary>
public class Order
{
    public string OrderId { get; set; } = Guid.NewGuid().ToString("N")[..12].ToUpper();
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ShippingAddress { get; set; }
    public string? PaymentMethod { get; set; }
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;
}

public class CreateOrderRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public string Currency { get; set; } = "USD";
    public string? ShippingAddress { get; set; }
    public string? PaymentMethod { get; set; }
}

public class OrderResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Order? Order { get; set; }
    public string? ErrorCode { get; set; }
}