namespace Shared.Models;

// --- Users ---
public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, string UserId, string Email, string Role);
public record UserResponse(string Id, string Name, string Email, string Role, DateTime CreatedAt);

// --- Products ---
public record CreateProductRequest(string Name, string Description, decimal Price, int Stock);
public record ProductResponse(string Id, string Name, string Description, decimal Price, int Stock, DateTime CreatedAt);

// --- Orders ---
public record CreateOrderRequest(string UserId, List<OrderItemRequest> Items);
public record OrderItemRequest(string ProductId, int Quantity);
public record OrderResponse(string Id, string UserId, List<OrderItem> Items, decimal Total, string Status, DateTime CreatedAt);

// --- Geral ---
public record ErrorResponse(string Error);
public record HealthResponse(string Status);
