# High-Throughput FlashSale Micro-Service

An ASP.NET Core 8 Web API for processing high-volume flash-sale orders using an in-memory bounded queue, background processing, PostgreSQL, pessimistic concurrency control, rate limiting, Polly retry, and automated xUnit tests.

## Technology Stack

- ASP.NET Core 8
- C#
- Entity Framework Core
- PostgreSQL
- Npgsql
- System.Threading.Channels
- Polly
- xUnit
- Swagger / OpenAPI

## Architecture

```text
Client
   |
   v
POST /api/orders
   |
   v
Request Validation
   |
   v
Rate Limiting
   |
   v
Bounded Channel Queue
   |
   v
BackgroundService
   |
   v
Scoped AppDbContext
   |
   v
PostgreSQL Transaction
   |
   v
SELECT ... FOR UPDATE
   |
   v
Stock Update + Order Creation
```

## API Endpoints

### Create Order

POST `/api/orders`

Request:

```json
{
  "productId": 1,
  "quantity": 1,
  "userId": "user101"
}
```

Header:

```text
X-User-Id: user101
```

Successful response:

```json
{
  "trackingId": "tracking-id",
  "status": "Pending"
}
```

HTTP status:

```text
202 Accepted
```

The order is placed into the background processing queue and processed asynchronously.

### Get Product Stock

GET `/api/products/{id}/stock`

Example:

```text
GET /api/products/1/stock
```

Response:

```json
{
  "productId": 1,
  "productName": "iPhone",
  "stock": 5
}
```

## Bounded Queue

Orders are stored in a bounded `System.Threading.Channels.Channel<OrderMessage>`.

Queue capacity:

```text
1000
```

The bounded queue prevents unlimited in-memory growth during high traffic.

The queue uses:

```text
BoundedChannelFullMode.Wait
```

so requests wait for queue capacity instead of silently dropping orders.

## Background Processing

`OrderProcessingWorker` inherits from `BackgroundService`.

The worker:

1. Reads orders from the channel.
2. Creates a new dependency-injection scope.
3. Resolves a scoped `AppDbContext`.
4. Starts a database transaction.
5. Locks the product row.
6. Checks available stock.
7. Updates stock when available.
8. Creates the order record.
9. Commits the transaction.

## Concurrency Control

PostgreSQL pessimistic locking is used:

```sql
SELECT *
FROM "Products"
WHERE "Id" = ...
FOR UPDATE
```

This locks the product row while the transaction is processing.

This prevents concurrent orders from overselling the available stock.

Example:

```text
Initial stock = 5

50 concurrent orders
       |
       v
5 orders  -> Success
45 orders -> OutOfStock

Final stock = 0
```

## Rate Limiting

Order requests are limited to:

```text
3 requests per user
within 10 seconds
```

The user is identified using:

```text
X-User-Id
```

Example:

```text
Request 1 -> 202
Request 2 -> 202
Request 3 -> 202
Request 4 -> 429 Too Many Requests
```

## Polly Retry

Database processing uses Polly retry with:

- Maximum retry attempts: 3
- Exponential backoff
- Jitter

This helps recover from temporary database failures.

## Dependency Injection Lifetimes

### Singleton

`OrderQueue`

The queue is shared by the API controllers and background worker.

### Scoped

`AppDbContext`

A new database context is created inside a scope for each order-processing operation.

### Hosted Service

`OrderProcessingWorker`

Runs continuously in the background and consumes queued orders.

## Automated Tests

xUnit tests are included.

### Concurrency Test

`Fifty_Parallel_Orders_Should_Not_Oversell`

The test sends 50 orders concurrently against a product with stock of 5.

Expected result:

```text
50 orders processed
5 successful orders
45 out-of-stock orders
Final stock = 0
```

### Polly Test

`Polly_Should_Retry_When_Operation_Fails`

The test simulates temporary failures and verifies that Polly retries the operation successfully.

## Running the Project

1. Install PostgreSQL.
2. Create the `FlashSaleDb` database.
3. Update the local PostgreSQL connection string.
4. Run EF Core migrations.
5. Start the ASP.NET Core API.
6. Open Swagger.
7. Test the API endpoints.

## Database Migrations

Run:

```powershell
Add-Migration InitialCreate
Update-Database
```

## Testing

Run all tests from:

```text
Visual Studio
→ Test
→ Test Explorer
→ Run All
```

Expected:

```text
2 Tests
2 Passed
0 Failed
```

## Security

Do not commit real database passwords, API keys, tokens, or other secrets to source control.

Use local configuration or environment variables for sensitive credentials.

## Project Structure

```text
FlashSale
│
├── FlashSale.Api
│   ├── Controllers
│   │   ├── OrdersController.cs
│   │   └── ProductsController.cs
│   │
│   ├── Data
│   │   └── AppDbContext.cs
│   │
│   ├── DTOs
│   │   ├── CreateOrderRequest.cs
│   │   └── OrderMessage.cs
│   │
│   ├── Models
│   │   ├── Order.cs
│   │   └── Product.cs
│   │
│   ├── Queue
│   │   └── OrderQueue.cs
│   │
│   ├── Workers
│   │   └── OrderProcessingWorker.cs
│   │
│   ├── Program.cs
│   └── appsettings.json
│
├── FlashSale.Tests
│   ├── FlashSaleConcurrencyTests.cs
│   └── PollyRetryTests.cs
│
└── README.md
```