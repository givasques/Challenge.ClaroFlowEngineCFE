using ClaroFlowEngine.Api.Modules.CustomerInsights;
using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Extensions;
using ClaroFlowEngine.Api.Common.Middleware;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Seed;
using ClaroFlowEngine.Api.Modules.Auth;
using ClaroFlowEngine.Api.Modules.Context;
using ClaroFlowEngine.Api.Modules.Handoff;
using ClaroFlowEngine.Api.Modules.Identity;
using ClaroFlowEngine.Api.Modules.Invoices;
using ClaroFlowEngine.Api.Modules.Lgpd;
using ClaroFlowEngine.Api.Modules.Opportunities;
using ClaroFlowEngine.Api.Modules.Panel;
using HealthChecks.NpgSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Serilog — logs estruturados em JSON (console + arquivo), lidos da configuração.
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    // FASE 4.3, item A.3: mascara CPF em qualquer propriedade de path de requisição, de qualquer
    // logger (Serilog.AspNetCore e o log nativo "Request finished" do ASP.NET Core) — ver CpfSafePathEnricher.
    .Enrich.With<CpfSafePathEnricher>()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
    .WriteTo.File(
        formatter: new Serilog.Formatting.Json.JsonFormatter(),
        path: "logs/cfe-.log",
        rollingInterval: RollingInterval.Day));

// Configuration binding — TTLs, limites operacionais, tokens de canal e URLs dos canais simulados.
builder.Services.AddOptions<CfeOptions>()
    .Bind(builder.Configuration.GetSection(CfeOptions.SectionName))
    .Validate(options => options.JourneyAttentionThresholdMinutes > 0,
        "Cfe:JourneyAttentionThresholdMinutes deve ser maior que zero.")
    .Validate(options => options.JourneyCriticalThresholdMinutes > options.JourneyAttentionThresholdMinutes,
        "Cfe:JourneyCriticalThresholdMinutes deve ser maior que JourneyAttentionThresholdMinutes.")
    .Validate(options => options.IsValidBusinessTimeZone(),
        "Cfe:BusinessTimeZoneId não é um fuso horário válido neste sistema.")
    .ValidateOnStart();
builder.Services.Configure<ChannelsOptions>(builder.Configuration.GetSection(ChannelsOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<PanelAuthOptions>(builder.Configuration.GetSection(PanelAuthOptions.SectionName));
builder.Services.Configure<AiSummaryOptions>(builder.Configuration.GetSection(AiSummaryOptions.SectionName));

// Falha rápido se a chave de assinatura do JWT estiver ausente ou curta demais — melhor não subir
// do que subir com segurança quebrada (FASE 4.1, item A.3).
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey ausente ou com menos de 32 caracteres. Configure a variável de ambiente " +
        "Jwt__SigningKey (ou appsettings.Development.json / user-secrets em dev) antes de subir a API.");
}

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

    // Esquema Bearer (FASE 4.1, item A.6) — habilita o botão "Authorize" no Swagger UI para testar
    // os endpoints do painel protegidos por JWT.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe apenas o token, sem o prefixo 'Bearer ' (o Swagger adiciona automaticamente).",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

// Registro de dependências por módulo (feature folders) e serviços compartilhados.
builder.Services.AddCommonServices();
builder.Services.AddIdentityModule();
builder.Services.AddContextModule();
builder.Services.AddHandoffModule();
builder.Services.AddInvoicesModule();
builder.Services.AddPanelModule();
builder.Services.AddLgpdModule();
builder.Services.AddOpportunitiesModule();
builder.Services.AddAuthModule();
builder.Services.AddCustomerInsightsModule();

// Autenticação JWT do painel (FASE 4.1, item A.3/A.6) — WhatsApp e App continuam com X-Channel-Token,
// resolvido pelo ChannelAuthMiddleware; só o painel passa a usar este esquema.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        // Por padrão, um 401/403 do JwtBearer sai com body vazio — sem isso o frontend não consegue
        // distinguir os casos pelo error_code, como o resto da API já faz (A.5).
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    new ApiError("invalid_or_expired_session", "Sessão inválida ou expirada. Entre novamente."));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    new ApiError("forbidden_for_role", "Seu perfil não tem acesso a este recurso."));
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Usada pela FASE 4.2 (dashboard do gestor); validada nesta fase só pelo endpoint GET /manager/ping (B.3).
    options.AddPolicy("ManagerOnly", policy => policy.RequireRole(PanelRole.Manager));
});

// Rate limiting (FASE 4.1, item A.7) — só no login, complementa o bloqueio por conta: protege contra
// tentativas em massa vindas do mesmo IP, inclusive testando e-mails diferentes.
var panelAuthOptions = builder.Configuration.GetSection(PanelAuthOptions.SectionName).Get<PanelAuthOptions>()
    ?? new PanelAuthOptions();
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ApiError("too_many_requests", "Muitas tentativas a partir desta rede. Aguarde um momento."),
            cancellationToken);
    };

    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = panelAuthOptions.LoginRateLimitPerMinute,
            Window = TimeSpan.FromMinutes(1),
        }));
});

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

// Sem chave, a API sobe e usa o resumo por regras; o aviso sai uma vez, na subida (FASE 4.4, A.2).
var aiSummaryOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiSummaryOptions>>().Value;
if (aiSummaryOptions.Enabled && aiSummaryOptions.Provider != "rules" && !aiSummaryOptions.IsRemoteProviderConfigured())
    app.Logger.LogWarning("IA não configurada; usando resumo por regras");

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
app.UseRateLimiter();
app.UseAuthentication();
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
