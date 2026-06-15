using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Shared.Auth;
using Shared.Data;
using Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Kestrel HTTPS configuration ---
builder.WebHost.ConfigureKestrel(options =>
{
    var certPath = Environment.GetEnvironmentVariable("CERT_PATH") ?? "certs/devcert.pfx";
    var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD") ?? "devpassword";

    options.ListenAnyIP(5003, listenOptions =>
    {
        listenOptions.UseHttps(certPath, certPassword);
    });
});

// --- JWT Authentication ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = JwtHelper.GetValidationParameters();
    });
builder.Services.AddAuthorization();

// --- JsonStore for orders ---
builder.Services.AddSingleton(new JsonStore<Order>("data/orders.json"));

// --- HttpClient with self-signed certificate handler ---
builder.Services.AddSingleton<HttpClient>(_ =>
{
    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    return new HttpClient(handler);
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// --- Service URLs ---
var usersServiceUrl = Environment.GetEnvironmentVariable("USERS_SERVICE_URL") ?? "https://localhost:5001";
var productsServiceUrl = Environment.GetEnvironmentVariable("PRODUCTS_SERVICE_URL") ?? "https://localhost:5002";

// --- JSON serializer options ---
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

// ============================================================
// POST /orders — Create a new order (requires JWT)
// ============================================================
app.MapPost("/orders", async (
    CreateOrderRequest request,
    HttpClient httpClient,
    JsonStore<Order> store,
    HttpContext context) =>
{
    // Extract userId from JWT claims
    var jwtUserId = context.User.FindFirstValue("userId");

    // Validate request
    if (string.IsNullOrWhiteSpace(request.UserId))
        return Results.BadRequest(new ErrorResponse("UserId é obrigatório"));

    if (request.Items == null || request.Items.Count == 0)
        return Results.BadRequest(new ErrorResponse("Pedido deve ter pelo menos 1 item"));

    foreach (var item in request.Items)
    {
        if (item.Quantity <= 0)
            return Results.BadRequest(new ErrorResponse($"Quantidade deve ser maior que 0 para o produto {item.ProductId}"));
    }

    // --- Validate user exists by calling Users service ---
    try
    {
        var userResponse = await httpClient.GetAsync($"{usersServiceUrl}/users/validate/{request.UserId}");
        if (userResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            return Results.BadRequest(new ErrorResponse($"Usuário {request.UserId} não encontrado"));

        userResponse.EnsureSuccessStatusCode();
    }
    catch (HttpRequestException ex)
    {
        return Results.Json(
            new ErrorResponse($"Serviço de usuários indisponível: {ex.Message}"),
            statusCode: 502);
    }

    // --- Validate each product and get prices ---
    var orderItems = new List<OrderItem>();
    decimal total = 0;

    foreach (var item in request.Items)
    {
        try
        {
            var productResponse = await httpClient.GetAsync($"{productsServiceUrl}/products/{item.ProductId}");
            if (productResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                return Results.BadRequest(new ErrorResponse($"Produto {item.ProductId} não encontrado"));

            productResponse.EnsureSuccessStatusCode();

            var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>(jsonOptions);
            if (product == null)
                return Results.BadRequest(new ErrorResponse($"Resposta inválida para produto {item.ProductId}"));

            var orderItem = new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                Price = product.Price
            };

            orderItems.Add(orderItem);
            total += product.Price * item.Quantity;
        }
        catch (HttpRequestException ex)
        {
            return Results.Json(
                new ErrorResponse($"Serviço de produtos indisponível: {ex.Message}"),
                statusCode: 502);
        }
    }

    // --- Create order ---
    var order = new Order
    {
        UserId = request.UserId,
        Items = orderItems,
        Total = total,
        Status = "confirmed"
    };

    store.Add(order);

    var response = new OrderResponse(
        order.Id,
        order.UserId,
        order.Items,
        order.Total,
        order.Status,
        order.CreatedAt);

    return Results.Created($"/orders/{order.Id}", response);
})
.RequireAuthorization();

// ============================================================
// GET /orders/{userId} — List orders by user (requires JWT)
// ============================================================
app.MapGet("/orders/{userId}", (
    string userId,
    JsonStore<Order> store,
    HttpContext context) =>
{
    // Validate that JWT userId matches the parameter
    var jwtUserId = context.User.FindFirstValue("userId");
    if (jwtUserId != userId)
        return Results.Json(
            new ErrorResponse("Acesso negado: você só pode ver seus próprios pedidos"),
            statusCode: 403);

    var orders = store.ReadAll()
        .Where(o => o.UserId == userId)
        .Select(o => new OrderResponse(
            o.Id,
            o.UserId,
            o.Items,
            o.Total,
            o.Status,
            o.CreatedAt))
        .ToList();

    return Results.Ok(orders);
})
.RequireAuthorization();

// ============================================================
// GET /health — Health check (no auth)
// ============================================================
app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")));

app.Run();
