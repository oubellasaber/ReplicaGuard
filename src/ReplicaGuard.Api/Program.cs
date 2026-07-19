using System.Text.Json.Serialization.Metadata;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using ReplicaGuard.Api.Extensions;
using ReplicaGuard.Api.Middleware;
using ReplicaGuard.Application;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Infrastructure;
using ReplicaGuard.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.TypeInfoResolverChain.Insert(0,
            new DefaultJsonTypeInfoResolver());
    });


builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOrMonitoringPolicy", policy =>
    {
        // The request must be authenticated AND meet one of these conditions
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(Roles.Admin));
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await app.ApplyMigrationsAsync();
    await app.SeedDataAsync();
}

// 1. Public Liveness Check (Fast, zero details)
app.MapHealthChecks("/healthz");

// 2. Protected Readiness Check (Deep dependencies)
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
}).RequireAuthorization("AdminOrMonitoringPolicy");

app.UseMiddleware<RequestContextLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program;
