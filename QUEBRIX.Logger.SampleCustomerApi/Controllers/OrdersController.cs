using Microsoft.AspNetCore.Mvc;
using Serilog;
using QUEBRIX.Logger.SampleCustomerApi.Models;

namespace QUEBRIX.Logger.SampleCustomerApi.Controllers;

/// <summary>
/// Sample Orders controller demonstrating various logging scenarios
/// with QUEBRIX Logger's structured logging capabilities.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private static readonly List<Order> Orders = new();
    private static readonly Random Rng = new();
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ILogger<OrdersController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a new order with detailed structured logging.
    /// </summary>
    [HttpPost]
    public IActionResult CreateOrder([FromBody] CreateOrderRequest request)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            Log.Warning("Order creation failed: missing CustomerId from {ClientIp}",
                HttpContext.Connection.RemoteIpAddress);

            return BadRequest(new OrderResponse
            {
                Success = false,
                Message = "Customer ID is required",
                ErrorCode = "MISSING_CUSTOMER_ID"
            });
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            Log.Warning("Order creation failed: no items in order for customer {CustomerId}",
                request.CustomerId);

            return BadRequest(new OrderResponse
            {
                Success = false,
                Message = "At least one item is required",
                ErrorCode = "EMPTY_ORDER"
            });
        }

        // Create order
        var order = new Order
        {
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            Items = request.Items,
            TotalAmount = request.Items.Sum(i => i.TotalPrice),
            Currency = request.Currency,
            ShippingAddress = request.ShippingAddress,
            PaymentMethod = request.PaymentMethod,
            Status = "Created"
        };

        Orders.Add(order);

        // ═══════════════════════════════════════════════════════════════
        // Structured logging with QUEBRIX - all properties are indexed
        // and searchable in the QUEBRIX Logger UI
        // ═══════════════════════════════════════════════════════════════
        Log.ForContext("OrderId", order.OrderId)
           .ForContext("CustomerId", order.CustomerId)
           .ForContext("CustomerName", order.CustomerName)
           .ForContext("ItemCount", order.Items.Count)
           .ForContext("TotalAmount", order.TotalAmount)
           .ForContext("Currency", order.Currency)
           .ForContext("PaymentMethod", order.PaymentMethod)
           .ForContext("ShippingAddress", order.ShippingAddress)
           .Information("Order {OrderId} created for customer {CustomerId} with {ItemCount} items totaling {TotalAmount} {Currency}");

        _logger.LogInformation("Order created: {OrderId}, Items: {ItemCount}, Total: {TotalAmount} {Currency}",
            order.OrderId, order.Items.Count, order.TotalAmount, order.Currency);

        return Ok(new OrderResponse
        {
            Success = true,
            Message = "Order created successfully",
            Order = order
        });
    }

    /// <summary>
    /// Retrieves an order by ID with audit logging.
    /// </summary>
    [HttpGet("{orderId}")]
    public IActionResult GetOrder(string orderId)
    {
        // Simulate some latency for demonstration
        Thread.Sleep(Rng.Next(10, 100));

        var order = Orders.FirstOrDefault(o => o.OrderId == orderId);

        if (order == null)
        {
            Log.Warning("Order {OrderId} not found - potential invalid reference from {ClientIp}",
                orderId, HttpContext.Connection.RemoteIpAddress);

            return NotFound(new OrderResponse
            {
                Success = false,
                Message = $"Order {orderId} not found",
                ErrorCode = "ORDER_NOT_FOUND"
            });
        }

        // Audit log - who accessed what
        Log.ForContext("OrderId", order.OrderId)
           .ForContext("CustomerId", order.CustomerId)
           .ForContext("Action", "ViewOrder")
           .ForContext("ClientIp", HttpContext.Connection.RemoteIpAddress?.ToString())
           .Information("Order {OrderId} viewed by customer {CustomerId}");

        return Ok(new OrderResponse
        {
            Success = true,
            Message = "Order retrieved",
            Order = order
        });
    }

    /// <summary>
    /// Lists orders with optional filtering.
    /// </summary>
    [HttpGet]
    public IActionResult ListOrders([FromQuery] string? customerId = null, [FromQuery] string? status = null)
    {
        var query = Orders.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(customerId))
            query = query.Where(o => o.CustomerId.Contains(customerId, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

        var results = query.ToList();

        Log.ForContext("FilterCustomerId", customerId ?? "(none)")
           .ForContext("FilterStatus", status ?? "(none)")
           .ForContext("ResultCount", results.Count)
           .Information("Orders listed: {ResultCount} results with filters [Customer: {FilterCustomerId}, Status: {FilterStatus}]");

        return Ok(new { TotalCount = results.Count, Orders = results });
    }

    /// <summary>
    /// Processes a payment for an order with error handling demonstration.
    /// </summary>
    [HttpPost("{orderId}/pay")]
    public IActionResult ProcessPayment(string orderId, [FromBody] PaymentRequest request)
    {
        var order = Orders.FirstOrDefault(o => o.OrderId == orderId);

        if (order == null)
        {
            return NotFound(new OrderResponse
            {
                Success = false,
                Message = $"Order {orderId} not found",
                ErrorCode = "ORDER_NOT_FOUND"
            });
        }

        if (order.Status != "Created")
        {
            Log.Warning("Cannot process payment for order {OrderId}: current status is {Status}",
                orderId, order.Status);

            return BadRequest(new OrderResponse
            {
                Success = false,
                Message = $"Cannot pay order with status '{order.Status}'",
                ErrorCode = "INVALID_STATUS"
            });
        }

        // Simulate payment processing with random success/failure
        var paymentSuccessful = Rng.Next(100) > 20; // 80% success rate

        if (!paymentSuccessful)
        {
            var errorCode = Rng.Next(2) switch
            {
                0 => "INSUFFICIENT_FUNDS",
                1 => "PAYMENT_GATEWAY_TIMEOUT",
                _ => "GENERIC_PAYMENT_ERROR"
            };

            // Error logging with full context to QUEBRIX
            Log.ForContext("OrderId", orderId)
               .ForContext("CustomerId", order.CustomerId)
               .ForContext("Amount", order.TotalAmount)
               .ForContext("Currency", order.Currency)
               .ForContext("PaymentMethod", request.PaymentMethod ?? order.PaymentMethod)
               .ForContext("ErrorCode", errorCode)
               .Error("Payment failed for order {OrderId}: {ErrorCode} (amount: {Amount} {Currency})");

            return BadRequest(new OrderResponse
            {
                Success = false,
                Message = $"Payment failed: {errorCode}",
                ErrorCode = errorCode
            });
        }

        order.Status = "Paid";

        // Successful payment logging
        Log.ForContext("OrderId", orderId)
           .ForContext("CustomerId", order.CustomerId)
           .ForContext("Amount", order.TotalAmount)
           .ForContext("Currency", order.Currency)
           .ForContext("PaymentMethod", request.PaymentMethod ?? order.PaymentMethod)
           .ForContext("TransactionId", Guid.NewGuid().ToString("N"))
           .Information("Payment of {Amount} {Currency} for order {OrderId} completed successfully");

        return Ok(new OrderResponse
        {
            Success = true,
            Message = "Payment processed successfully",
            Order = order
        });
    }

    /// <summary>
    /// Triggers a simulated exception to demonstrate error logging.
    /// </summary>
    [HttpGet("trigger-error")]
    public IActionResult TriggerError()
    {
        try
        {
            // Simulate an unexpected error
            throw new InvalidOperationException("Simulated database connection timeout after 30 seconds");
        }
        catch (Exception ex)
        {
            // Full exception logging with stack trace to QUEBRIX
            Log.ForContext("ErrorSource", "OrdersController.TriggerError")
               .ForContext("MachineName", Environment.MachineName)
               .ForContext("UserId", User?.Identity?.Name ?? "anonymous")
               .Error(ex, "Unhandled exception in OrdersController: {ErrorMessage}", ex.Message);

            return StatusCode(500, new OrderResponse
            {
                Success = false,
                Message = "An unexpected error occurred. The error has been logged.",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }
}

public class PaymentRequest
{
    public string? PaymentMethod { get; set; }
    public string? TransactionReference { get; set; }
}