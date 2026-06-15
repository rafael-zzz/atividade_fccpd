using Microsoft.AspNetCore.Authentication.JwtBearer;
using Products.Services;
using Shared.Auth;
using Shared.Models;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// --- Kestrel HTTPS configuration ---
builder.WebHost.ConfigureKestrel(options =>
{
    var certPath = Environment.GetEnvironmentVariable("CERT_PATH") ?? "certs/devcert.pfx";
    var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD") ?? "devpassword";

    options.ListenAnyIP(5002, listenOptions =>
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

// --- Replicated Store ---
var store = new ReplicatedStore("data/replica1.json", "data/replica2.json");
builder.Services.AddSingleton(store);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// --- GET /products — list all products (round-robin read, no auth) ---
app.MapGet("/products", (ReplicatedStore store) =>
{
    var products = store.ReadAll();
    var response = products.Select(p => new ProductResponse(
        p.Id, p.Name, p.Description, p.Price, p.Stock, p.CreatedAt
    )).ToList();
    return Results.Ok(response);
});

// --- GET /products/{id} — get product by id (round-robin read, no auth) ---
app.MapGet("/products/{id}", (string id, ReplicatedStore store) =>
{
    var products = store.ReadAll();
    var product = products.FirstOrDefault(p => p.Id == id);
    if (product is null)
        return Results.NotFound(new ErrorResponse("Produto não encontrado"));

    return Results.Ok(new ProductResponse(
        product.Id, product.Name, product.Description, product.Price, product.Stock, product.CreatedAt
    ));
});

// --- POST /products — create product (requires JWT with role=admin) ---
app.MapPost("/products", (CreateProductRequest request, ReplicatedStore store, HttpContext context) =>
{
    // Check authentication
    var user = context.User;
    if (user.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    // Check admin role
    var role = user.FindFirst(ClaimTypes.Role)?.Value;
    if (role != "admin")
        return Results.Json(new ErrorResponse("Apenas administradores podem criar produtos"), statusCode: 403);

    var product = new Product
    {
        Id = Guid.NewGuid().ToString(),
        Name = request.Name,
        Description = request.Description,
        Price = request.Price,
        Stock = request.Stock,
        CreatedAt = DateTime.UtcNow
    };

    // Add writes to BOTH replicas synchronously (strong consistency)
    store.Add(product);

    var response = new ProductResponse(
        product.Id, product.Name, product.Description, product.Price, product.Stock, product.CreatedAt
    );
    return Results.Created($"/products/{product.Id}", response);
}).RequireAuthorization();

// --- GET /health — health check ---
app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")));

app.Run();
