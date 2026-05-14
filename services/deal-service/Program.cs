using DealService.Data;
using DealService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console()
       .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://seq:80"));

builder.Services.AddDealServiceDependencies(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddDbContextCheck<DealDbContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DealDbContext>();
    db.Database.Migrate();
}

app.UseSerilogRequestLogging();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
