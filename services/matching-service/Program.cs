using MatchingService.Data;
using MatchingService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console()
       .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://seq:80"));

builder.Services.AddMatchingServiceDependencies(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddDbContextCheck<MatchingDbContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MatchingDbContext>();
    db.Database.Migrate();
}

app.UseSerilogRequestLogging();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
