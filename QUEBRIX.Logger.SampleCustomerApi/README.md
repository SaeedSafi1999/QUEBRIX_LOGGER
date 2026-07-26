# QUEBRIX Logger - Sample Customer API

This is a **sample ASP.NET Core Web API** that demonstrates how a customer application integrates with **QUEBRIX Logger** as a drop-in replacement for Seq.

## What It Demonstrates

### 1. Serilog + QUEBRIX Sink Configuration (`Program.cs`)
- Configures Serilog with `WriteTo.QUEBRIX()` - the drop-in replacement for `WriteTo.Seq()`
- Reads configuration from `appsettings.json` section `QuebrixLogger`
- Demonstrates all available sink options: batching, compression, buffering, retry, tags, custom headers

### 2. Controllers with Structured Logging

| Controller | Endpoint | Logging Features |
|-----------|----------|-----------------|
| **OrdersController** | `POST /api/orders` | Validation warnings, structured properties (OrderId, CustomerId, Amount), business event logging |
| | `GET /api/orders/{id}` | Audit logging, not-found warnings |
| | `POST /api/orders/{id}/pay` | Error logging with error codes, successful payment events |
| | `GET /api/orders/trigger-error` | Full exception logging with stack traces |
| **CustomersController** | `POST /api/customers` | Verbose/Debug/Information level examples |
| | `PATCH /api/customers/{id}/tier` | Warning-level logging for notable changes |
| | `GET /api/customers/simulate-crash` | Fatal-level critical error simulation |
| **DiagnosticsController** | `GET /api/diagnostics/test-all-levels` | Tests all 6 log levels (Verbose through Fatal) |
| | `GET /api/diagnostics/test-structured-logging` | Tests rich structured properties (numbers, booleans, arrays, dictionaries) |
| | `GET /api/diagnostics/test-exception-logging` | Tests nested exception logging with full stack traces |
| | `POST /api/diagnostics/generate-batch` | Generates N log events in a batch for performance testing |

### 3. Request/Response Logging Middleware
- `RequestResponseLoggingMiddleware.cs` - Logs every HTTP request/response with method, path, status code, duration, client IP, user agent

### 4. Background Service for Simulated Logs
- `SimulatedLogGeneratorService.cs` - Generates realistic log events every 2-8 seconds with varied levels (50% Info, 25% Debug, 15% Warning, 8% Error, 2% Fatal)

## How to Run

```bash
# Start the QUEBRIX Logger server (required)
dotnet run --project ../QUEBRIX.Logger.Server

# In another terminal, start this sample API
dotnet run --project .
```

Then open your browser to `http://localhost:5000/api/diagnostics/health`

### Test the Integration

```bash
# Test all log levels
curl http://localhost:5000/api/diagnostics/test-all-levels

# Test structured logging
curl http://localhost:5000/api/diagnostics/test-structured-logging

# Test exception logging
curl http://localhost:5000/api/diagnostics/test-exception-logging

# Create an order (requires QUEBRIX server to be running)
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"CUST001","customerName":"John Doe","customerEmail":"john@example.com","items":[{"productId":"PROD-1","productName":"Widget","quantity":2,"unitPrice":19.99}]}'

# Generate batch of 100 logs
curl -X POST "http://localhost:5000/api/diagnostics/generate-batch?count=100"

# View all logs in QUEBRIX UI at http://localhost:5298
```

## Configuration

All QUEBRIX Logger settings are in `appsettings.json` under the `QuebrixLogger` section:

```json
{
  "QuebrixLogger": {
    "Url": "http://localhost:8080",
    "ApiKey": "your-api-key",
    "Application": "SampleCustomerApi",
    "Environment": "Development",
    "Tags": ["sample", "demo"],
    "BatchSize": 50,
    "FlushPeriodSeconds": 5,
    "UseCompression": true,
    "MaxRetries": 3
  }
}
```

Environment variables can override any setting using the `QUEBRIX_` prefix, e.g.:
```bash
set QUEBRIX_URL=http://quebrix-server:8080
set QUEBRIX_APIKEY=my-production-key