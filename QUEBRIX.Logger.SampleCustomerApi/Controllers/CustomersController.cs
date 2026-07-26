using Microsoft.AspNetCore.Mvc;
using Serilog;
using QUEBRIX.Logger.SampleCustomerApi.Models;

namespace QUEBRIX.Logger.SampleCustomerApi.Controllers;

/// <summary>
/// Sample Customers controller demonstrating QUEBRIX Logger usage
/// with different log levels and structured properties.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private static readonly List<Customer> Customers = new();
    private static readonly Random Rng = new();

    /// <summary>
    /// Creates a new customer.
    /// </summary>
    [HttpPost]
    public IActionResult CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            Log.Verbose("Customer creation request missing email field"); // Verbose - very detailed
            return BadRequest(new { Error = "Email is required" });
        }

        // Check for duplicate
        if (Customers.Any(c => c.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
        {
            Log.Debug("Attempted to create duplicate customer with email {Email}", request.Email); // Debug - diagnostic
            return Conflict(new { Error = "Customer with this email already exists" });
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            Tier = request.Tier ?? "Standard",
            CreatedAt = DateTime.UtcNow
        };

        Customers.Add(customer);

        // Information - normal business event
        Log.ForContext("CustomerId", customer.Id)
           .ForContext("Email", customer.Email)
           .ForContext("Tier", customer.Tier)
           .Information("Customer {CustomerId} ({Email}) created with tier {Tier}");

        return CreatedAtAction(nameof(GetCustomer), new { customerId = customer.Id }, customer);
    }

    /// <summary>
    /// Gets a customer by ID.
    /// </summary>
    [HttpGet("{customerId}")]
    public IActionResult GetCustomer(string customerId)
    {
        var customer = Customers.FirstOrDefault(c => c.Id == customerId);

        if (customer == null)
        {
            return NotFound(new { Error = $"Customer {customerId} not found" });
        }

        return Ok(customer);
    }

    /// <summary>
    /// Lists all customers.
    /// </summary>
    [HttpGet]
    public IActionResult ListCustomers()
    {
        Log.Information("Customers listed: {Count} total customers", Customers.Count);
        return Ok(new { TotalCount = Customers.Count, Customers });
    }

    /// <summary>
    /// Updates customer tier (demonstrates warning-level logging).
    /// </summary>
    [HttpPatch("{customerId}/tier")]
    public IActionResult UpdateTier(string customerId, [FromBody] UpdateTierRequest request)
    {
        var customer = Customers.FirstOrDefault(c => c.Id == customerId);
        if (customer == null)
            return NotFound(new { Error = "Customer not found" });

        if (string.IsNullOrWhiteSpace(request.Tier))
            return BadRequest(new { Error = "Tier is required" });

        var oldTier = customer.Tier;
        customer.Tier = request.Tier;

        // Warning - something noteworthy but not an error
        Log.ForContext("CustomerId", customerId)
           .ForContext("OldTier", oldTier)
           .ForContext("NewTier", request.Tier)
           .Warning("Customer {CustomerId} tier changed from {OldTier} to {NewTier}");

        return Ok(customer);
    }

    /// <summary>
    /// Simulates a fatal error scenario for demonstration.
    /// </summary>
    [HttpGet("simulate-crash")]
    public IActionResult SimulateCrash()
    {
        Log.Fatal("Critical system failure simulated in CustomersController! Immediate attention required!");

        // Also tag with high-priority for alerting
        Log.ForContext("Severity", "Critical")
           .ForContext("RequiresImmediateAction", true)
           .Fatal("Simulated unrecoverable error - application state may be corrupted");

        return StatusCode(500, new { Error = "Simulated crash - check QUEBRIX logs for details" });
    }
}

public class Customer
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Tier { get; set; } = "Standard";
    public DateTime CreatedAt { get; set; }
}

public class CreateCustomerRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Tier { get; set; }
}

public class UpdateTierRequest
{
    public string Tier { get; set; } = string.Empty;
}