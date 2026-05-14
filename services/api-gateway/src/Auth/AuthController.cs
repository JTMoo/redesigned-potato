using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Auth;

[ApiController]
public sealed class AuthController : ControllerBase
{
    private readonly JwtService _jwtService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AuthController(JwtService jwtService, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(jwtService);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        _jwtService = jwtService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("/auth/google")]
    public IActionResult SignIn()
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "Auth");
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUrl },
            GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("/signin-google")]
    public async Task<IActionResult> GoogleCallback()
    {
        var result = await HttpContext.AuthenticateAsync();
        if (!result.Succeeded)
            return BadRequest("Authentication failed");

        var googleId = result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var email = result.Principal.FindFirstValue(ClaimTypes.Email)!;
        var name = result.Principal.FindFirstValue(ClaimTypes.Name) ?? email;

        var userId = await EnsureUserExistsAsync(googleId, email, name);
        var token = _jwtService.Issue(userId, email, name);

        var frontendUrl = _configuration["FRONTEND_URL"] ?? "http://localhost:3000";
        return Redirect($"{frontendUrl}/auth/callback?token={token}");
    }

    private async Task<string> EnsureUserExistsAsync(string googleId, string email, string name)
    {
        var client = _httpClientFactory.CreateClient("user-service");
        var response = await client.PostAsJsonAsync("/users/upsert", new
        {
            GoogleId = googleId,
            Email = email,
            DisplayName = name,
        });

        if (!response.IsSuccessStatusCode)
            return Guid.NewGuid().ToString();

        var body = await response.Content.ReadFromJsonAsync<UserUpsertResponse>();
        return body?.Id ?? Guid.NewGuid().ToString();
    }

    private sealed record UserUpsertResponse(string Id);
}
