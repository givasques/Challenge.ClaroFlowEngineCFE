using ClaroFlowEngine.Api.Common.Middleware;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Seed;
using ClaroFlowEngine.Api.Modules.Context;
using ClaroFlowEngine.Api.Modules.Handoff;
using ClaroFlowEngine.Api.Modules.Identity;
using HealthChecks.NpgSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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
builder.Services.AddHandoffModule();

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

// Ordem do pipeline conforme padroes-e-boas-praticas.md §13:
// correlationId -> exceptionHandling -> logging -> cors -> channelAuth -> authorization -> controllers.
app.UseCorrelationId();
app.UseExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

// Serve os canais simulados (HTML/CSS/JS) em /channels/*. Usado no modo "full" (docker-compose.full.yml),
// onde a própria API entrega os arquivos estáticos; em dev, os canais normalmente rodam via http-server
// à parte (portas 5171/5173/5175), então isso é só um bônus opcional — não quebra nada se a pasta não existir.
var channelsPathConfig = app.Configuration["StaticFiles:ChannelsPath"] ?? "../../channels";
var channelsFullPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, channelsPathConfig));
if (Directory.Exists(channelsFullPath))
{
    var channelsFileProvider = new PhysicalFileProvider(channelsFullPath);

    // UseDefaultFiles resolve "/channels/whatsapp-sim/" -> "/channels/whatsapp-sim/index.html".
    // Sem isso, só a URL com o nome do arquivo explícito funciona — UseStaticFiles sozinho não faz esse fallback.
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = channelsFileProvider,
        RequestPath = "/channels",
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = channelsFileProvider,
        RequestPath = "/channels",
    });
    app.Logger.LogInformation("Servindo canais estáticos de {Path} em /channels", channelsFullPath);
}
else
{
    app.Logger.LogWarning(
        "Pasta de canais não encontrada em {Path} — /channels não será servido pela API.", channelsFullPath);
}

app.UseCors();
app.UseChannelAuth();
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
