using LifeManager.Infrastructure.DI;
using LifeManager.Application.DI;
using LifeManager.WebApi;
using LifeManager.WebApi.DI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

var connectionString = builder.Configuration["lifeManagerConnectionString"]
    ?? throw new InvalidOperationException("Environment variable [lifeManagerConnectionString] not found");

builder.Services.AddInfrastructureServices(connectionString);

builder.Services.AddApplicationServices();

builder.AddApiServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();