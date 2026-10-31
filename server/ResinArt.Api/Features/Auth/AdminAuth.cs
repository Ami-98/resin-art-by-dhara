using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ResinArt.Api.Infrastructure.Settings;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Features.Auth;

public sealed class AdminUserDocument : IDocument
{
    public string Id { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Admin";
    public int FailedLogins { get; set; }
    public DateTimeOffset? LockoutUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public sealed class AdminAuthService(
    IDocumentStore<AdminUserDocument> users,
    IOptionsMonitor<AdminOptions> options,
    ILogger<AdminAuthService> logger)
{
    private readonly PasswordHasher<AdminUserDocument> _hasher = new();

    public AdminOptions CurrentOptions => options.CurrentValue;

    public async Task SeedDefaultAdminIfEmptyAsync(CancellationToken ct = default)
    {
        if (await users.CountAsync(ct: ct) > 0) return;

        var opts = CurrentOptions;
        var admin = new AdminUserDocument
        {
            Id = opts.InitialUserId,
            Username = opts.InitialUsername,
            DisplayName = opts.InitialDisplayName,
            Role = opts.Role
        };
        admin.PasswordHash = _hasher.HashPassword(admin, opts.InitialPassword);

        await users.UpsertAsync(admin, ct: ct);
        logger.LogInformation("Seeded initial admin account '{Username}'.", opts.InitialUsername);
    }

    public async Task<(AdminUserDocument? User, string? Error)> VerifyCredentialsAsync(
        string username,
        string password,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (null, "Username and password are required.");

        var matches = await users.QueryAsync(
            u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase), ct);
        var user = matches.FirstOrDefault();
        if (user is null)
            return (null, "Invalid username or password.");

        var opts = CurrentOptions;
        var now = DateTimeOffset.UtcNow;
        if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > now)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((user.LockoutUntil.Value - now).TotalMinutes));
            return (null, $"Account is temporarily locked due to failed login attempts. Try again in {minutes} min.");
        }

        var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLogins++;
            if (user.FailedLogins >= opts.MaxFailedAttempts)
            {
                var lockoutDuration = TimeSpan.FromMinutes(Math.Max(1, opts.LockoutMinutes));
                user.LockoutUntil = now.Add(lockoutDuration);
                logger.LogWarning("Admin account '{Username}' locked out for {Minutes} minutes.", user.Username, lockoutDuration.TotalMinutes);
            }
            await users.UpsertAsync(user, ct: ct);
            return (null, "Invalid username or password.");
        }

        user.FailedLogins = 0;
        user.LockoutUntil = null;
        user.LastLoginAt = now;
        await users.UpsertAsync(user, ct: ct);
        return (user, null);
    }

    public async Task<(bool Ok, string? Error)> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        var minLen = Math.Max(6, CurrentOptions.MinPasswordLength);
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < minLen)
            return (false, $"New password must be at least {minLen} characters.");

        var user = await users.GetAsync(userId, ct);
        if (user is null)
            return (false, "Admin account not found.");

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            return (false, "Current password is incorrect.");

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        await users.UpsertAsync(user, ct: ct);
        return (true, null);
    }
}

public static class AdminAuthEndpoints
{
    public const string CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string AdminPolicy = "Admin";

    public static IEndpointRouteBuilder MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest req,
            HttpContext http,
            AdminAuthService auth,
            CancellationToken ct) =>
        {
            var (user, error) = await auth.VerifyCredentialsAsync(req.Username, req.Password, ct);
            if (user is null)
            {
                return Results.Problem(
                    title: error ?? "Invalid credentials.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.Username),
                new("display_name", user.DisplayName),
                new(ClaimTypes.Role, user.Role)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieScheme));
            var expireHours = Math.Max(1, auth.CurrentOptions.SessionExpireHours);

            await http.SignInAsync(CookieScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(expireHours),
                AllowRefresh = true
            });

            return Results.Ok(new AdminProfileDto(user.Id, user.Username, user.DisplayName, user.Role));
        }).WithTags("Auth");

        app.MapPost("/api/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieScheme);
            return Results.Ok(new { message = "Signed out." });
        }).WithTags("Auth");

        app.MapGet("/api/auth/me", (ClaimsPrincipal user, IOptionsMonitor<AdminOptions> adminOptions) =>
        {
            if (user.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            return Results.Ok(new AdminProfileDto(
                Id: user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                Username: user.Identity.Name ?? "",
                DisplayName: user.FindFirstValue("display_name") ?? user.Identity.Name ?? "",
                Role: user.FindFirstValue(ClaimTypes.Role) ?? adminOptions.CurrentValue.Role));
        }).WithTags("Auth");

        app.MapPost("/api/auth/change-password", async (
            ChangePasswordRequest req,
            ClaimsPrincipal principal,
            AdminAuthService auth,
            CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();

            var (ok, error) = await auth.ChangePasswordAsync(userId, req.CurrentPassword, req.NewPassword, ct);
            if (!ok)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = [error ?? "Could not change password."]
                });
            }

            return Results.Ok(new { message = "Password updated." });
        })
        .RequireAuthorization(AdminPolicy)
        .WithTags("Auth");

        return app;
    }
}

public sealed record LoginRequest(string Username, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AdminProfileDto(string Id, string Username, string DisplayName, string Role);
