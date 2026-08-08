using ClaroFlowEngine.Api.Common.Middleware;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Seed;
using ClaroFlowEngine.Api.Modules.Context;
using ClaroFlowEngine.Api.Modules.Identity;
using HealthChecks.NpgSql;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Serilog — logs estruturados em JSON (console + arquivo), lidos da configuração.
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
    .WriteTo.File(
        formatter: new Serilog.Formatting.Json.JsonFormatter(),
        path: "logs/cfe-.log",
        rollingInterval: RollingInterval.Day));

// Configuration binding — TTLs, tokens de canal e URLs dos canais simulados.
builder.Services.Configure<CfeOptions>(builder.Configuration.GetSection(CfeOptions.SectionName));
builder.Services.Configure<ChannelsOptions>(builder.Configuration.GetSection(ChannelsOptions.SectionName));

// EF Core + PostgreSQL. UseSnakeCaseNamingConvention converte PascalCase -> snake_case automaticamente.
builder.Services.AddDbContext<CfeDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
    .UseSnakeCaseNamingConvention());

// JSON em snake_case nas respostas da API, conforme convenção definida na spec técnica.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // DTOs de mesmo nome existem em módulos diferentes (ex: CustomerSummaryDto em Identity e em Context),
    // por design — evita acoplar um módulo a DTOs de outro. Usar o nome completo evita colisão de schemaId.
    options.CustomSchemaIds(type => type.FullName);
});

// Registro de dependências por módulo (feature folders) e serviços compartilhados.
builder.Services.AddCommonServices();
builder.Services.AddIdentityModule();
builder.Services.AddContextModule();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!, name: "db");

var channelsConfig = builder.Configuration.GetSection(ChannelsOptions.SectionName).Get<ChannelsOptions>()
    ?? new ChannelsOptions();
var allowedOrigins = new[]
{
    channelsConfig.WhatsappSimBaseUrl,
    channelsConfig.AppSimBaseUrl,
    channelsConfig.AttendantPanelBaseUrl
}.Where(url => !string.IsNullOrWhiteSpace(url)).ToArray();

builder.Services.AddCors(opt => opt.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Aplica migrations pendentes e roda o seed automaticamente em dev/staging.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        db.Database.Migrate();
        await DatabaseSeeder.SeedAsync(db);
    }
}

// Middleware de exceções primeiro no pipeline: captura qualquer erro dos middlewares/controllers seguintes.
app.UseExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => e.Value.Status.ToString().ToLowerInvariant())
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});

app.Run();
