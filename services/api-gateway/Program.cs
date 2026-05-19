using System.Security.Claims;
using System.Text;
using ApiGateway.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console()
       .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://seq:80"));

var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException("JWT_SECRET is required");
var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["GOOGLE_CLIENT_ID"]
        ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID is required");
    options.ClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"]
        ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET is required");
    options.CallbackPath = "/signin-google";
    // Hardcode the redirect URI so it is always correct regardless of whether
    // the request arrives directly (port 8080) or through the nginx proxy (port 3000)
    // where the Host header has no port attached.
    var redirectUri = builder.Configuration["GOOGLE_REDIRECT_URI"]
        ?? "http://localhost:8080/signin-google";
    options.Events.OnRedirectToAuthorizationEndpoint = ctx =>
    {
        var uri = new UriBuilder(ctx.RedirectUri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        query["redirect_uri"] = redirectUri;
        uri.Query = query.ToString();
        ctx.Response.Redirect(uri.ToString());
        return Task.CompletedTask;
    };
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = jwtKey,
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero,
    };
});

builder.Services.AddHttpClient("user-service", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:UserService"]
        ?? "http://user-service:8081"));

builder.Services.AddSingleton<JwtService>();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseAuthentication();

app.Use(async (context, next) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    if (authHeader?.StartsWith("Bearer ") == true)
    {
        var token = authHeader["Bearer ".Length..];
        var jwtService = context.RequestServices.GetRequiredService<JwtService>();
        var userId = jwtService.TryExtractUserId(token);
        if (userId is not null)
            context.Request.Headers["X-User-Id"] = userId;
    }
    await next();
});

app.MapControllers();
app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();
