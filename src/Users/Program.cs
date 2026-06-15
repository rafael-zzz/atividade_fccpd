using Microsoft.AspNetCore.Authentication.JwtBearer;
using Shared.Auth;
using Shared.Data;
using Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Kestrel HTTPS ---
builder.WebHost.ConfigureKestrel(options =>
{
    var certPath = Environment.GetEnvironmentVariable("CERT_PATH") ?? "certs/devcert.pfx";
    var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD") ?? "devpassword";

    options.ListenAnyIP(5001, listenOptions =>
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

// --- JsonStore<User> via DI ---
builder.Services.AddSingleton<JsonStore<User>>(
    _ => new JsonStore<User>("data/users.json"));

var app = builder.Build();

// --- Seed admin user ---
var store = app.Services.GetRequiredService<JsonStore<User>>();
var users = store.ReadAll();
if (users.Count == 0)
{
    var admin = new User
    {
        Name = "Admin",
        Email = "admin@admin.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
        Role = "admin"
    };
    store.Add(admin);
    Console.WriteLine($"[Seed] Admin user created: {admin.Email}");
}

app.UseAuthentication();
app.UseAuthorization();

// ==================== ENDPOINTS ====================

// POST /users/register
app.MapPost("/users/register", (RegisterRequest request, JsonStore<User> store) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) ||
        string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new ErrorResponse("Nome, email e senha são obrigatórios"));
    }

    var existing = store.ReadAll();
    if (existing.Any(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Conflict(new ErrorResponse("Email já cadastrado"));
    }

    var user = new User
    {
        Name = request.Name,
        Email = request.Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        Role = "user"
    };

    store.Add(user);

    var response = new UserResponse(user.Id, user.Name, user.Email, user.Role, user.CreatedAt);
    return Results.Created($"/users/{user.Id}", response);
});

// POST /users/login
app.MapPost("/users/login", (LoginRequest request, JsonStore<User> store) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new ErrorResponse("Email e senha são obrigatórios"));
    }

    var user = store.ReadAll()
        .FirstOrDefault(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase));

    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
        return Results.Json(new ErrorResponse("Email ou senha inválidos"), statusCode: 401);
    }

    var token = JwtHelper.GenerateToken(user.Id, user.Email, user.Role);
    var response = new LoginResponse(token, user.Id, user.Email, user.Role);
    return Results.Ok(response);
});

// GET /users/{id} — requires JWT
app.MapGet("/users/{id}", (string id, JsonStore<User> store) =>
{
    var user = store.ReadAll().FirstOrDefault(u => u.Id == id);
    if (user is null)
    {
        return Results.NotFound(new ErrorResponse("Usuário não encontrado"));
    }

    var response = new UserResponse(user.Id, user.Name, user.Email, user.Role, user.CreatedAt);
    return Results.Ok(response);
}).RequireAuthorization();

// GET /users/validate/{id} — internal endpoint for service-to-service validation (no auth)
app.MapGet("/users/validate/{id}", (string id, JsonStore<User> store) =>
{
    var user = store.ReadAll().FirstOrDefault(u => u.Id == id);
    if (user is null)
    {
        return Results.NotFound(new ErrorResponse("Usuário não encontrado"));
    }

    return Results.Ok(new { exists = true, id = user.Id });
});

// GET /health
app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")));

app.Run();
