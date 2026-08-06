# Padrões e Boas Práticas — Backend e Banco de Dados

**Escopo:** desenvolvimento de APIs REST em .NET com PostgreSQL, focado em produtividade sustentável, código legível e evolução segura.
**Público-alvo:** desenvolvedor(a) e/ou agente de IA responsável pela codificação.
**Convenção geral:** código, nomes técnicos e commits em **inglês**; comentários, mensagens ao usuário final e documentação em **português**.

---

# Parte I — Backend (.NET / C#)

## 1. Nomenclatura

| Elemento | Convenção | Exemplo |
|---|---|---|
| Classes, records, structs, enums | `PascalCase` | `JourneyContext`, `IdentityService` |
| Interfaces | `IPascalCase` | `IIdentityService` |
| Métodos | `PascalCase` | `ResolveAsync`, `OpenJourney` |
| Propriedades públicas | `PascalCase` | `CustomerId`, `CreatedAt` |
| Campos privados | `_camelCase` | `_dbContext`, `_logger` |
| Parâmetros e variáveis locais | `camelCase` | `customerId`, `journeyId` |
| Constantes | `PascalCase` (ou `SCREAMING_SNAKE_CASE` para constantes globais) | `MaxRetries`, `DefaultTimeoutSeconds` |
| Namespaces | `PascalCase.PorPontos` | `ClaroFlowEngine.Api.Modules.Identity` |
| Arquivos | Um tipo público por arquivo, mesmo nome do tipo | `IdentityService.cs` contém `class IdentityService` |
| Métodos assíncronos | Sufixo `Async` obrigatório | `ResolveAsync`, `GetByIdAsync` |
| Booleanos | Prefixos `Is`, `Has`, `Can`, `Should` | `IsActive`, `HasChildren`, `CanBeUpdated` |

**Nomes descritivos, não abreviados:**

```csharp
// Bom
public async Task<Customer?> GetCustomerByIdAsync(Guid customerId)

// Ruim
public async Task<Customer?> Get(Guid id)  // ambíguo
public async Task<Customer?> GetCustBy(Guid cid)  // abreviação sem ganho
```

**Não repita o nome do tipo em métodos:**

```csharp
// Bom (dentro de CustomerService)
public async Task<Customer> GetByIdAsync(Guid id)

// Ruim
public async Task<Customer> GetCustomerByIdAsync(Guid id)  // redundante dentro de CustomerService
```

---

## 2. Estrutura e organização

**Um projeto principal, organizado por módulos (feature folders), não por tipo.**

```
src/
└── ProjectName.Api/
    ├── Program.cs
    ├── Modules/
    │   ├── Feature1/
    │   │   ├── Controllers/
    │   │   ├── Services/
    │   │   ├── Dtos/
    │   │   └── Validators/
    │   └── Feature2/
    ├── Data/
    │   ├── DbContext.cs
    │   ├── Entities/
    │   ├── Configurations/
    │   ├── Migrations/
    │   └── Seed/
    ├── Common/
    │   ├── Middleware/
    │   ├── Errors/
    │   ├── Extensions/
    │   └── Contracts/
    └── Configuration/
```

**Por que feature folders e não `Controllers/`, `Services/`, `Models/` no topo?**

Feature folders escalam melhor: para mudar algo em Identity, tudo está numa pasta. Em projetos organizados por tipo, uma feature se espalha em 4-5 lugares e o custo cognitivo de navegação cresce rápido.

**Regra de dependência:**

- `Modules/*` podem depender de `Data/`, `Common/`.
- `Common/` não depende de nada específico do domínio.
- `Data/` não depende de `Modules/*`.
- `Modules/A` **não deve** depender diretamente de `Modules/B` — se precisar, mova a lógica compartilhada para `Common/`.

---

## 3. Camadas e responsabilidades

O padrão mínimo saudável:

```
Controller ─▶ Service ─▶ DbContext / External API
   (HTTP)     (Regra)         (Persistência)
```

### Controller

**Responsabilidade:** receber requisição HTTP, validar shape (via DTO + validação), chamar Service, montar resposta HTTP.

**Não coloque:**
- Regra de negócio
- Acesso direto ao DbContext
- Cálculos, formatações complexas

```csharp
[ApiController]
[Route("identity")]
public class IdentityController : ControllerBase
{
    private readonly IIdentityService _service;

    public IdentityController(IIdentityService service) => _service = service;

    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve(
        [FromBody] ResolveIdentityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ResolveAsync(request, cancellationToken);
        return Ok(result);
    }
}
```

### Service

**Responsabilidade:** orquestrar a operação de negócio, aplicar regras, transacionar quando necessário, gerar eventos/logs.

**Não coloque:**
- Detalhes HTTP (`HttpContext`, headers, status codes)
- Serialização/desserialização manual
- Acesso ao filesystem sem abstração

```csharp
public class IdentityService : IIdentityService
{
    private readonly CfeDbContext _db;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(CfeDbContext db, ILogger<IdentityService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ResolveIdentityResponse> ResolveAsync(
        ResolveIdentityRequest request,
        CancellationToken cancellationToken)
    {
        // regra de negócio aqui
    }
}
```

### Repository (opcional para o MVP)

O `DbContext` do EF Core já é um Unit of Work + Repositories genéricos. Só adicione uma camada de Repository dedicada quando:
- Vai substituir o ORM no futuro
- Query lógica está sendo duplicada em vários services
- Testabilidade exige mock de acesso a dados

Para projetos pequenos/MVPs, **usar `DbContext` diretamente no Service é aceitável e recomendado**.

---

## 4. DTOs e mapeamento

**Regra absoluta:** nunca retorne uma Entity diretamente do Controller. Sempre mapeie para um DTO.

**Por quê:**
- Entities têm campos internos (`CreatedBy`, `RowVersion`, navegações) que não devem vazar.
- Loops de serialização (parent → child → parent) causam StackOverflow em JSON.
- Mudanças de schema não podem quebrar contrato de API silenciosamente.

**Padrão de DTOs:**

```csharp
// Request DTO
public record OpenJourneyRequest(
    Guid CustomerId,
    string OriginChannel,
    string Intent,
    string InitialStep,
    Dictionary<string, object>? Payload);

// Response DTO
public record JourneyResponse(
    Guid Id,
    Guid CustomerId,
    string OriginChannel,
    string Intent,
    string CurrentStep,
    Dictionary<string, object> Payload,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

**Mapeamento manual é aceitável e claro em MVPs:**

```csharp
private static JourneyResponse ToResponse(JourneyContext entity) => new(
    entity.Id,
    entity.CustomerId,
    entity.OriginChannel,
    entity.Intent,
    entity.CurrentStep,
    entity.Payload,
    entity.Status,
    entity.CreatedAt,
    entity.UpdatedAt);
```

`AutoMapper` só compensa em projetos com muitos DTOs e muita repetição. Para até ~20 mapeamentos, código manual é mais legível.

**`record` em vez de `class` para DTOs:** ganha imutabilidade, `with` expressions, `Equals` estrutural e menos boilerplate.

---

## 5. Injeção de dependência

**Registre por tempo de vida correto:**

| Tempo de vida | Quando usar | Exemplo |
|---|---|---|
| `AddSingleton` | Uma instância pela aplicação inteira; sem estado mutável ou estado thread-safe | `IOptions<T>`, caches, factories |
| `AddScoped` | Uma instância por request | Services, DbContext, unit-of-work |
| `AddTransient` | Nova instância a cada resolução | Objetos leves, sem estado |

**No `Program.cs`, organize por módulo:**

```csharp
// Program.cs
builder.Services.AddIdentityModule();
builder.Services.AddContextModule();
builder.Services.AddHandoffModule();

// Modules/Identity/IdentityModuleExtensions.cs
public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IIdentityService, IdentityService>();
        return services;
    }
}
```

Isso mantém o `Program.cs` legível e a inicialização de cada módulo próxima do próprio módulo.

**Nunca use `service locator` (`GetService<T>()` em runtime dentro de código de negócio).** Sempre injete no construtor.

---

## 6. Configuração

**Nunca hardcode valores que podem mudar entre ambientes.** Use `appsettings.json` + `IOptions<T>`.

**Padrão para uma seção de config:**

```csharp
// Configuration/CfeOptions.cs
public class CfeOptions
{
    public const string SectionName = "Cfe";

    public int HandoffTokenTtlMinutes { get; set; } = 30;
    public int JourneyInactivityTtlHours { get; set; } = 24;
    public string[] AllowedChannelTokens { get; set; } = [];
}

// Program.cs
builder.Services.Configure<CfeOptions>(
    builder.Configuration.GetSection(CfeOptions.SectionName));

// Uso no service
public class HandoffService(IOptions<CfeOptions> options)
{
    private readonly CfeOptions _cfe = options.Value;

    public void GenerateToken()
    {
        var ttl = TimeSpan.FromMinutes(_cfe.HandoffTokenTtlMinutes);
    }
}
```

**Hierarquia de configuração:**

1. `appsettings.json` — valores padrão versionados (sem segredos).
2. `appsettings.{Environment}.json` — sobrescreve por ambiente (Development, Staging, Production).
3. Variáveis de ambiente — sobrescreve tudo. Padrão para segredos.
4. `dotnet user-secrets` — segredos locais em dev, fora do repositório.

**Nunca versione:**
- `appsettings.Development.json` com segredos
- Connection strings de produção
- Chaves de API, JWT secrets, etc.

**No `.gitignore`:**

```
appsettings.Development.json
appsettings.Production.json
.env
.env.*
```

---

## 7. Tratamento de erros

**Exceptions de domínio + middleware global.**

Defina exceções de domínio que carregam intenção:

```csharp
// Common/Errors/DomainException.cs
public abstract class DomainException : Exception
{
    public string ErrorCode { get; }
    public object? Details { get; }

    protected DomainException(string errorCode, string message, object? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Details = details;
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string errorCode, string message)
        : base(errorCode, message) { }
}

public class ConflictException : DomainException { /* ... */ }
public class GoneException : DomainException { /* ... */ }
public class ValidationException : DomainException { /* ... */ }
```

**Middleware global:**

```csharp
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            var status = ex switch
            {
                NotFoundException => 404,
                ConflictException => 409,
                GoneException => 410,
                ValidationException => 400,
                _ => 500
            };

            _logger.LogWarning(ex, "Domain exception: {ErrorCode}", ex.ErrorCode);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error_code = ex.ErrorCode,
                message = ex.Message,
                details = ex.Details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new
            {
                error_code = "internal_error",
                message = "Ocorreu um erro inesperado."
            });
        }
    }
}
```

**No service, lance a exceção com contexto útil:**

```csharp
if (journey is null)
    throw new NotFoundException("journey_not_found", $"Jornada {id} não encontrada.");

if (journey.Status != JourneyStatus.Open)
    throw new ConflictException("journey_not_open",
        $"Jornada está no estado '{journey.Status}' e não pode ser atualizada.");
```

**Nunca:**
- Capture exceção genérica sem re-lançar ou logar.
- Retorne `null` para indicar erro (use exceptions ou `Result<T>`).
- Vaze stack trace ou detalhes de infraestrutura em respostas de erro.

---

## 8. Validação de entrada

Valide **cedo, no controller ou no service**, antes de qualquer chamada custosa.

**Opção 1 — Data Annotations (para casos simples):**

```csharp
public class ResolveIdentityRequest
{
    [Required]
    [RegularExpression("^(whatsapp|app|cpf|call)$")]
    public string Channel { get; set; } = "";

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Identifier { get; set; } = "";
}
```

**Opção 2 — FluentValidation (para regras mais complexas):**

```csharp
public class ResolveIdentityRequestValidator : AbstractValidator<ResolveIdentityRequest>
{
    public ResolveIdentityRequestValidator()
    {
        RuleFor(x => x.Channel).NotEmpty().Must(BeSupportedChannel)
            .WithMessage("Canal não suportado.");

        When(x => x.Channel == "cpf", () =>
        {
            RuleFor(x => x.Identifier).Matches(@"^\d{11}$")
                .WithMessage("CPF deve conter 11 dígitos.");
        });
    }

    private bool BeSupportedChannel(string ch)
        => new[] { "whatsapp", "app", "cpf", "call" }.Contains(ch);
}
```

**Regra prática:** validação de formato/shape no Controller (Data Annotations ou FluentValidation). Validação de regra de negócio (ex: "cliente não pode ter duas jornadas abertas") no Service.

---

## 9. Async e cancelamento

**Todo método I/O bound é `async`.**

```csharp
// Bom
public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct)
    => await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

// Ruim (bloqueia thread)
public Customer? GetById(Guid id)
    => _db.Customers.FirstOrDefault(c => c.Id == id);
```

**Propague `CancellationToken` até o fim.**

Isso é importante porque quando o cliente HTTP desconecta, o `.NET` sinaliza o `CancellationToken` e você economiza recursos abortando queries em andamento.

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
{
    var result = await _service.GetByIdAsync(id, cancellationToken);
    return Ok(result);
}
```

**Nunca:**

```csharp
// Nunca faça .Result ou .Wait() em código async — deadlock e desperdício de thread.
var result = _service.GetByIdAsync(id).Result;  // ❌
_service.GetByIdAsync(id).Wait();  // ❌

// Nunca use async void, exceto em event handlers.
public async void DoSomething() { }  // ❌
public async Task DoSomething() { }  // ✅
```

**Métodos async devem terminar com `Async`.**

---

## 10. Convenções REST

**Verbos e semântica:**

| Verbo | Uso | Idempotente | Retorno típico |
|---|---|---|---|
| `GET` | Ler recurso(s) | ✅ | 200 (achou), 404 (não achou) |
| `POST` | Criar recurso ou ação | ❌ | 201 (criado), 200 (ação), 400/409 |
| `PUT` | Substituir recurso inteiro | ✅ | 200 ou 204 |
| `PATCH` | Atualizar parcial | Depende | 200 ou 204 |
| `DELETE` | Remover recurso | ✅ | 204 (sem body) ou 200 |

**URLs baseadas em recursos (substantivos, não verbos):**

```
✅ GET  /customers/{id}
✅ POST /customers/{id}/orders
✅ POST /journeys/{id}/close     ← ação sobre recurso

❌ GET  /getCustomer?id=...
❌ POST /createOrder
❌ POST /closeJourney?id=...
```

**Plural para coleções:**

```
✅ /customers, /orders, /journeys
❌ /customer, /order, /journey
```

**Status codes que você realmente precisa saber:**

| Código | Significado | Quando usar |
|---|---|---|
| 200 | OK | Sucesso genérico com body |
| 201 | Created | Criação de recurso (retorne também `Location` header) |
| 204 | No Content | Sucesso sem body (típico em DELETE, PATCH sem retorno) |
| 400 | Bad Request | Erro de validação, payload malformado |
| 401 | Unauthorized | Não autenticado |
| 403 | Forbidden | Autenticado mas sem permissão |
| 404 | Not Found | Recurso não existe |
| 409 | Conflict | Estado atual incompatível com a operação |
| 410 | Gone | Recurso expirou/foi removido permanentemente |
| 422 | Unprocessable Entity | Payload válido mas regra de negócio recusou (alternativa a 400) |
| 429 | Too Many Requests | Rate limit |
| 500 | Internal Server Error | Erro não tratado no servidor |
| 503 | Service Unavailable | Dependência crítica indisponível (banco fora) |

**Nunca use 200 para erro.** Se a operação falhou, use o código apropriado.

**Convenção de payload JSON — snake_case ou camelCase:**

Decida uma convenção e mantenha. `snake_case` tem sido mais adotado em APIs REST modernas (Stripe, GitHub, etc.). No .NET:

```csharp
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});
```

---

## 11. Segurança

**HTTPS sempre em produção. Em dev, HTTP local é aceitável.**

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
```

**Nunca hardcode segredos.** Ver §6.

**Não confie no cliente:**
- Valide tudo do lado do servidor.
- Nunca use `.HasIdParameter(userId)` vindo do request sem cross-checar com o usuário autenticado.
- Cheque autorização em cada endpoint que exige.

**Proteja contra vulnerabilidades comuns:**

- **SQL Injection:** use sempre parâmetros/queries parametrizadas. EF Core faz isso por padrão; se usar `FromSqlRaw`, use interpolação segura.
  ```csharp
  // ✅ Seguro
  var result = await _db.Customers.FromSqlInterpolated(
      $"SELECT * FROM customers WHERE cpf = {cpf}").ToListAsync();

  // ❌ Vulnerável
  var result = await _db.Customers.FromSqlRaw(
      $"SELECT * FROM customers WHERE cpf = '{cpf}'").ToListAsync();
  ```

- **Mass assignment:** use DTOs específicos por endpoint. Não aceite `Customer` inteiro se o cliente só deveria mudar o nome.

- **Rate limiting:** em produção, configure `AddRateLimiter` para prevenir abuso.

- **CORS:** configure com origens explícitas, nunca `.AllowAnyOrigin()` em produção.

**Headers de segurança em produção:**

```csharp
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
```

**Autenticação em MVP vs produção:**

- **MVP:** mock com header simples é aceitável, mas **deixe comentário explícito** de que é mock.
- **Produção:** JWT (via `AddJwtBearer`), OAuth2, ou provedor de identidade dedicado.

---

## 12. Logging estruturado

**Log é estrutura, não string.**

```csharp
// ✅ Estruturado
_logger.LogInformation(
    "Journey opened for customer {CustomerId} on {Channel} with intent {Intent}",
    customerId, channel, intent);

// ❌ String interpolada
_logger.LogInformation(
    $"Journey opened for customer {customerId} on {channel} with intent {intent}");
```

Estruturado permite queries como `select where CustomerId = 'xxx'` no sink de logs (Seq, Elasticsearch, etc.). Interpolação vira uma string opaca.

**Níveis de log:**

| Nível | Uso |
|---|---|
| `Trace` | Detalhe fino, quase nunca em produção |
| `Debug` | Diagnóstico durante desenvolvimento |
| `Information` | Fluxo normal de negócio (jornada aberta, requisição processada) |
| `Warning` | Situação anormal mas tratada (retry, cache miss, degradação) |
| `Error` | Falha que impediu a operação |
| `Critical` | Falha que afeta o serviço inteiro (banco fora, disco cheio) |

**Sempre inclua correlation id.**

Middleware que gera um GUID por request e injeta no `LogContext`:

```csharp
public class CorrelationIdMiddleware
{
    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        ctx.Response.Headers["X-Correlation-Id"] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(ctx);
        }
    }
}
```

**O que logar:**

- Início e fim de operações significativas (com IDs).
- Falhas (com contexto).
- Decisões importantes (ex: "token expirado, jornada marcada como expired").

**O que NÃO logar:**

- Payload completo em cada request (excesso de volume).
- Senhas, tokens, dados sensíveis (CPF completo é discutível — mascare).
- Loops apertados.

---

## 13. Middleware

**Ordem importa.** O pipeline é uma pilha: o primeiro adicionado é o primeiro a ver a request e o último a ver a response.

Ordem típica saudável:

```csharp
app.UseCorrelationId();          // 1. correlation id primeiro
app.UseExceptionHandling();       // 2. captura tudo que vier abaixo
app.UseSerilogRequestLogging();   // 3. logging de request/response
app.UseHttpsRedirection();        // 4. só se HTTPS
app.UseCors();                    // 5. CORS antes de auth
app.UseAuthentication();          // 6. autenticação
app.UseAuthorization();           // 7. autorização (depois de auth)
app.MapControllers();             // 8. rotas por último
```

**Middleware customizado:** um por arquivo, com nome descritivo, e uma extensão para registrar:

```csharp
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
```

---

## 14. Documentação da API

**Swagger/OpenAPI habilitado por padrão em dev.**

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

**Documente na anotação, não só na spec externa:**

```csharp
/// <summary>
/// Resolve a identidade unificada do cliente para um par (canal, identificador).
/// Se o identificador não existir, cria um novo link.
/// </summary>
/// <response code="200">Identidade resolvida com sucesso.</response>
/// <response code="400">Identificador em formato inválido.</response>
/// <response code="404">CPF não cadastrado e sem hint para criação.</response>
[HttpPost("resolve")]
[ProducesResponseType(typeof(ResolveIdentityResponse), 200)]
[ProducesResponseType(typeof(ApiError), 400)]
[ProducesResponseType(typeof(ApiError), 404)]
public async Task<IActionResult> Resolve(...)
```

Habilite XML comments no `.csproj`:

```xml
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

**Regra de ouro:** se um novo dev conseguir bater os endpoints via Swagger UI e entender o que cada um faz sem ter que ler o código, a documentação está boa.

---

## 15. Idempotência

**Endpoints de criação devem tolerar retentativa.**

Se o cliente enviar o mesmo `POST /orders` duas vezes por erro de rede, você não deve criar dois pedidos.

Estratégias:

**A. Detecção por chave natural do request:**

```csharp
// Se já existe jornada open com esse customer+intent, retorna a existente
var existing = await _db.JourneyContexts
    .FirstOrDefaultAsync(j =>
        j.CustomerId == request.CustomerId &&
        j.Intent == request.Intent &&
        j.Status == JourneyStatus.Open, ct);

if (existing is not null)
    return ToResponse(existing);
```

**B. Idempotency-Key header (para APIs mais robustas):**

```
POST /orders
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

Você armazena a chave + resultado por 24h. Se a mesma chave chegar de novo, retorna o resultado original sem reprocessar.

**Endpoints de leitura (GET) são sempre idempotentes por definição.**

**DELETE idempotente:** deletar recurso já deletado retorna 204 (sucesso), não 404.

---

## 16. Performance

**Otimize quando precisar, não antes.** Mas evite armadilhas óbvias desde o começo.

**N+1 queries — o erro mais comum em EF Core:**

```csharp
// ❌ N+1: uma query para pegar clientes, N queries para pegar planos
var customers = await _db.Customers.ToListAsync();
foreach (var c in customers)
{
    var plans = c.CustomerPlans;  // lazy load = query extra
}

// ✅ Include ou Projection
var customers = await _db.Customers
    .Include(c => c.CustomerPlans)
    .ThenInclude(cp => cp.Plan)
    .ToListAsync();
```

**Use `AsNoTracking()` para leitura pura:**

```csharp
// ✅ Sem overhead de change tracking
var journeys = await _db.JourneyContexts
    .AsNoTracking()
    .Where(j => j.Status == "open")
    .ToListAsync();
```

**Projeção com DTO/select — traz só o que precisa:**

```csharp
var summaries = await _db.JourneyContexts
    .Where(j => j.CustomerId == customerId)
    .Select(j => new JourneySummary(j.Id, j.Intent, j.Status))
    .ToListAsync();
```

**Paginação em listagens:**

Nunca retorne "todas as jornadas" sem limite. Sempre paginê.

```csharp
public record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

var query = _db.JourneyContexts.AsNoTracking();
var total = await query.CountAsync();
var items = await query
    .OrderByDescending(j => j.CreatedAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(j => ToDto(j))
    .ToListAsync();
```

**Cache quando faz sentido:**

- Dados que mudam pouco (planos, configurações) → `IMemoryCache` local.
- Dados compartilhados entre instâncias → Redis (`IDistributedCache`).
- Cache com invalidação clara. Cache eterno vira bug.

---

# Parte II — Banco de Dados (PostgreSQL)

## 1. Nomenclatura

| Elemento | Convenção | Exemplo |
|---|---|---|
| Tabelas | `snake_case`, plural | `customers`, `journey_contexts` |
| Colunas | `snake_case` | `full_name`, `created_at`, `customer_id` |
| Chave primária | `id` (sem prefixo do nome da tabela) | `customers.id`, não `customer_id` |
| Chave estrangeira | `{tabela_singular}_id` | `customer_id`, `plan_id` |
| Índices | `ix_{tabela}_{colunas}` | `ix_journey_contexts_customer_id` |
| Índices únicos | `ux_{tabela}_{colunas}` | `ux_customers_cpf` |
| Constraints | `ck_{tabela}_{regra}` | `ck_customers_cpf_length` |
| Foreign keys | `fk_{tabela}_{tabela_ref}` | `fk_journey_contexts_customers` |

**Nunca use nomes reservados** (`user`, `order`, `type`, `group`) sem escapar. Prefira renomear (`users` → `app_users`, `orders` → `customer_orders`).

**Não abrevie sem necessidade:**

```
✅ customers, journey_contexts, handoff_tokens
❌ custs, jrnctx, hoff_tk
```

---

## 2. Tipos de dados

**Escolha o tipo certo para o dado, não o mais genérico.**

| Dado | Tipo recomendado (Postgres) | Justificativa |
|---|---|---|
| ID | `UUID` (para sistemas distribuídos) ou `BIGSERIAL` (para monolítico) | UUID: gerável no cliente, sem coordenação. BIGSERIAL: menor, mais rápido em índices. |
| Texto curto | `VARCHAR(N)` com N sensato | Limite explícito documenta expectativa |
| Texto longo | `TEXT` | Sem limite prático |
| Boolean | `BOOLEAN` | Nunca `INT` com 0/1 |
| Data + hora | `TIMESTAMPTZ` | Sempre com timezone. Nunca `TIMESTAMP` puro. |
| Data pura | `DATE` | |
| Duração | `INTERVAL` | |
| Dinheiro | `NUMERIC(precision, scale)` ou centavos em `INT/BIGINT` | Nunca `FLOAT`/`DOUBLE` (perde precisão) |
| Enum de negócio | `VARCHAR` + `CHECK` constraint | Mais flexível que `ENUM` nativo para adicionar valores |
| JSON semi-estruturado | `JSONB` | `JSONB` indexa e faz query eficiente; `JSON` só armazena |

**TIMESTAMPTZ sempre.**

Postgres armazena `TIMESTAMPTZ` como UTC no disco e converte na entrada/saída de acordo com o timezone da sessão. Isso evita bugs terríveis quando servidor e cliente estão em fusos diferentes.

**Dinheiro em centavos ou NUMERIC — nunca float:**

```sql
-- ✅ Centavos como INT — sem imprecisão de float
monthly_price_cents INT NOT NULL

-- ✅ NUMERIC com precisão explícita
monthly_price NUMERIC(10, 2) NOT NULL

-- ❌ Float — perde centavos silenciosamente
monthly_price FLOAT NOT NULL
```

---

## 3. Chaves primárias e estrangeiras

**Toda tabela tem chave primária.** Sem exceções.

**Escolha entre UUID e BIGSERIAL:**

| | UUID | BIGSERIAL |
|---|---|---|
| Tamanho | 16 bytes | 8 bytes |
| Gerável no cliente | ✅ | ❌ (banco define) |
| Ordenável | ⚠️ (só UUIDv7) | ✅ |
| Enumeração impossível externamente | ✅ | ❌ (endpoints com `/orders/1234` expõem volume) |
| Melhor em índices | ❌ (fragmenta B-tree) | ✅ |
| Uso recomendado | APIs públicas, sistemas distribuídos, mobile | Sistemas internos, tabelas de alto volume |

Para o CFE e maioria dos MVPs modernos: **UUID** (com `gen_random_uuid()` — extensão `pgcrypto`).

**Chaves estrangeiras têm índice explícito.**

Postgres **não** cria índice automático em FKs (ao contrário do MySQL). Sem o índice, JOINs e cascatas ficam lentos.

```sql
CREATE TABLE journey_contexts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id),
    ...
);
CREATE INDEX ix_journey_contexts_customer_id ON journey_contexts(customer_id);
```

**ON DELETE — decisão consciente:**

```sql
-- CASCADE: deletar customer apaga tudo dele. Bom para dados dependentes.
customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE

-- RESTRICT (padrão): erra ao deletar customer com filhos. Bom para integridade.
customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE RESTRICT

-- SET NULL: deixa filhos órfãos. Bom para auditoria.
customer_id UUID REFERENCES customers(id) ON DELETE SET NULL
```

Documente a escolha. Nunca fique no padrão sem pensar.

---

## 4. Índices

**Índice acelera leitura, custa em escrita.** Cada `INSERT`, `UPDATE`, `DELETE` mantém todos os índices — o custo cresce linearmente.

**Crie índice quando:**

- A coluna aparece frequentemente em `WHERE`, `JOIN`, `ORDER BY`.
- Cardinalidade é alta (muitos valores distintos). Índice em coluna com 3 valores possíveis é quase inútil.
- Consultas são lentas mensuravelmente (`EXPLAIN ANALYZE` mostra `Seq Scan`).

**NÃO crie índice:**

- "Por precaução" em toda coluna.
- Em colunas de texto longo (use `GIN` com trigram ou full-text search).
- Duplicando o que já é coberto por outro índice (ex: se tem índice em `(a, b)`, não precisa de índice em `a`).

**Índice composto — ordem importa:**

```sql
-- Serve para: WHERE customer_id = ? AND status = ?
-- Serve para: WHERE customer_id = ?
-- NÃO serve para: WHERE status = ?
CREATE INDEX ix_journey_customer_status ON journey_contexts(customer_id, status);
```

Regra: coloque primeiro a coluna mais seletiva ou a mais consultada isoladamente.

**Índice parcial — filtra linhas do próprio índice:**

Se 99% das jornadas são `concluded` e você quase sempre busca `open`:

```sql
CREATE INDEX ix_journey_open_updated
ON journey_contexts(updated_at)
WHERE status = 'open';
```

O índice fica pequeno, atualiza rápido e cobre exatamente o caso comum.

**Verifique uso periodicamente:**

```sql
-- Índices nunca usados
SELECT indexrelname, idx_scan
FROM pg_stat_user_indexes
WHERE idx_scan = 0;
```

---

## 5. Constraints

**Constraint no banco > validação só na aplicação.**

Aplicação pode ter bug, ter múltiplas instâncias com regras diferentes, ou o banco pode ser acessado por outro sistema. Constraint no banco é a última linha de defesa.

**Tipos essenciais:**

```sql
-- NOT NULL — obrigatoriedade
full_name VARCHAR(200) NOT NULL

-- UNIQUE — unicidade
cpf VARCHAR(11) UNIQUE NOT NULL

-- CHECK — regra de negócio simples
CONSTRAINT ck_customers_cpf_format CHECK (cpf ~ '^\d{11}$')
CONSTRAINT ck_plans_price_positive CHECK (monthly_price_cents > 0)

-- UNIQUE composta
CONSTRAINT ux_identity_links_channel_identifier UNIQUE (channel, identifier)

-- FOREIGN KEY
CONSTRAINT fk_journey_customer FOREIGN KEY (customer_id) REFERENCES customers(id)
```

**Enums via CHECK:**

```sql
status VARCHAR(20) NOT NULL,
CONSTRAINT ck_journey_status CHECK (status IN ('open', 'concluded', 'expired', 'abandoned'))
```

Mais flexível que `CREATE TYPE ... AS ENUM` porque adicionar valor não exige `ALTER TYPE`.

---

## 6. Timestamps e auditoria

**Toda tabela tem `created_at`, quase toda tem `updated_at`.**

```sql
CREATE TABLE journey_contexts (
    ...
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

**`updated_at` automático via trigger:**

```sql
CREATE OR REPLACE FUNCTION set_updated_at() RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_journey_contexts_updated_at
BEFORE UPDATE ON journey_contexts
FOR EACH ROW EXECUTE FUNCTION set_updated_at();
```

Se preferir controlar via aplicação, seja consistente — nunca esqueça de setar em algum `UPDATE`.

**Para auditoria mais rica (quem fez o quê):**

- Adicione `created_by`, `updated_by` referenciando `users(id)`.
- Ou tabela separada de eventos/audit log (`entity_type`, `entity_id`, `action`, `user_id`, `changes JSONB`, `occurred_at`).

O CFE usa o approach de tabela `journey_transitions` — o histórico é o próprio audit log da jornada.

---

## 7. Migrations

**Toda mudança de schema é uma migration.** Sem exceção. Nunca altere schema manualmente em produção.

**Regras:**

1. Migrations são **imutáveis** após committadas. Nunca edite uma migration já aplicada em algum ambiente. Crie uma nova para corrigir.
2. Migrations são **sequenciais**. O EF Core garante ordem via timestamp no nome.
3. Migrations devem ser **idempotentes se possível** — rodar duas vezes não quebra.
4. **Teste rollback** quando possível (`dotnet ef migrations remove`).
5. **Nomeie descritivamente:** `AddJourneyExpirationColumn`, não `Migration42`.

**No EF Core:**

```bash
# Criar migration
dotnet ef migrations add AddHandoffTokenTable

# Ver SQL que será gerado (antes de aplicar!)
dotnet ef migrations script

# Aplicar
dotnet ef database update
```

**Sempre revise o SQL gerado antes de aplicar em ambiente compartilhado.**

**Migrations grandes → divida:**

Se uma migration cria tabela + copia dados + drop de coluna antiga, quebre em passos. Facilita rollback e debug.

---

## 8. Queries seguras

**SQL injection é o mais evitável — e ainda um dos ataques mais comuns.**

**Sempre use parâmetros:**

```csharp
// ✅ Parametrizado
await _db.Customers.FromSqlInterpolated(
    $"SELECT * FROM customers WHERE cpf = {cpf}").ToListAsync();

// ✅ Melhor: LINQ tipado
await _db.Customers.Where(c => c.Cpf == cpf).ToListAsync();

// ❌ Concatenação — SQL injection
await _db.Customers.FromSqlRaw(
    $"SELECT * FROM customers WHERE cpf = '{cpf}'").ToListAsync();
```

**Nunca construa SQL com string interpolada não parametrizada, nem em ferramentas admin.**

**Least privilege:** o usuário do banco que a aplicação usa **não deve ser superuser**. Deve ter permissão só para as tabelas que precisa.

---

## 9. Transações

**Use transação quando mudanças em múltiplas tabelas devem ser atômicas.**

Exemplo: abrir jornada + registrar transição de início. Se a segunda falhar, a primeira não deve persistir.

```csharp
await using var transaction = await _db.Database.BeginTransactionAsync(ct);
try
{
    _db.JourneyContexts.Add(journey);
    _db.JourneyTransitions.Add(transition);
    await _db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
}
catch
{
    await transaction.RollbackAsync(ct);
    throw;
}
```

**Regra da menor transação possível:** transações longas seguram locks e degradam concorrência. Nunca faça I/O externo (chamada HTTP, e-mail) dentro de transação.

**Isolation levels — o padrão do Postgres (`READ COMMITTED`) resolve 95% dos casos.** Só suba para `REPEATABLE READ` ou `SERIALIZABLE` se identificar condição de corrida real.

---

## 10. Normalização e JSONB

**Normalize por padrão. Use JSONB para casos específicos.**

**Normalize quando:**
- Você consulta/filtra pelos campos individualmente.
- Os campos têm relacionamentos claros com outras entidades.
- Você faz agregações (`SUM`, `AVG`) sobre os campos.

**Use JSONB quando:**
- Estrutura varia por linha (ex: payload de jornada muda por intent).
- Campos raramente entram em queries ou filtros.
- Modelar em tabelas relacionais criaria mais complexidade que valor.

**JSONB no Postgres é poderoso:**

```sql
-- Query em campo JSONB
SELECT * FROM journey_contexts
WHERE payload ->> 'selected_plan_code' = 'claro_60gb';

-- Índice em campo específico do JSONB
CREATE INDEX ix_journey_selected_plan ON journey_contexts
    USING GIN ((payload -> 'selected_plan_code'));
```

**Anti-padrão:** guardar tudo em uma tabela `data JSONB` gigante para "flexibilidade". Você perde constraint checking, relacionamentos, performance de query estruturada, e ganha nada.

---

## 11. Soft delete

**Soft delete = marcar como deletado (`deleted_at`), não remover fisicamente.**

**Use quando:**
- Auditoria/histórico é importante (LGPD, compliance).
- Dados podem precisar ser restaurados.
- Outras tabelas referenciam a linha e você não quer cascade.

**Como implementar:**

```sql
ALTER TABLE customers ADD COLUMN deleted_at TIMESTAMPTZ;
CREATE INDEX ix_customers_active ON customers(id) WHERE deleted_at IS NULL;
```

Toda query padrão filtra: `WHERE deleted_at IS NULL`.

**No EF Core:**

```csharp
modelBuilder.Entity<Customer>()
    .HasQueryFilter(c => c.DeletedAt == null);
```

**Cuidados:**

- Constraint `UNIQUE` em coluna passa a considerar deletados também — use `UNIQUE (cpf) WHERE deleted_at IS NULL` (índice único parcial).
- Não confunda com anonimização. LGPD pode exigir remoção real ou anonimização de campos sensíveis, não só marca de deletado.

---

## 12. Retenção e LGPD

**Não guarde dados pessoais para sempre por padrão.**

Para cada tipo de dado sensível, defina:
- **Retenção:** por quanto tempo mantém em ativo? Em histórico?
- **Anonimização:** quando o dado deixa de ser identificável (CPF → hash)?
- **Exclusão sob solicitação:** como responder a um pedido de esquecimento?

**Padrões técnicos:**

- Campos sensíveis (CPF, nome completo, telefone, e-mail) marcados no schema (comentário ou coluna dedicada em tabela de metadados).
- Endpoint `DELETE /customers/{id}` que anonimiza campos sensíveis (não apenas soft delete) e mantém apenas dados de auditoria não identificáveis.
- Log de acessos a dados pessoais (quem, quando, por quê).
- Encryption at rest do banco (responsabilidade da infra).
- Encryption in transit (TLS obrigatório).

**Não armazene o que não precisa.** Se você não usa a data de nascimento, não peça.

---

## 13. Anti-padrões a evitar

| Anti-padrão | Por quê evitar |
|---|---|
| `SELECT *` em produção | Traz colunas que você não usa, custa I/O, quebra silenciosamente ao adicionar coluna |
| Ausência de FK "porque é mais rápido" | Corrompe integridade referencial; ganho de performance é desprezível |
| Chave primária composta em tabela transacional | Complica JOINs, FKs, ORMs; prefira `id` sintético + `UNIQUE` na chave natural |
| Guardar CPF/telefone como `BIGINT` | Perde zeros à esquerda, obriga formatação; sempre `VARCHAR` |
| `VARCHAR(255)` genérico | Não documenta expectativa; use tamanho real |
| Nome de coluna genérico (`data`, `info`, `value`, `type`) | Descreve o que a coluna significa, não o tipo dela |
| Enums com valores mágicos (`status = 1`) | Torne legível: `status = 'open'` |
| Trigger que executa lógica de negócio complexa | Difícil de debugar, testar, versionar; prefira aplicação |
| Coluna nullable "por precaução" | Se não pode ser nula, marque `NOT NULL` |
| Índice em toda coluna | Custo de escrita cresce sem ganho de leitura |
| Migrations editadas após aplicadas | Quebra histórico, imprevisível em ambientes múltiplos |

---

# Parte III — Integração Backend ↔ Banco (EF Core)

## 1. Configuração do DbContext

```csharp
public class CfeDbContext : DbContext
{
    public CfeDbContext(DbContextOptions<CfeDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<JourneyContext> JourneyContexts => Set<JourneyContext>();
    // ...

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CfeDbContext).Assembly);
    }
}
```

**No `Program.cs`:**

```csharp
builder.Services.AddDbContext<CfeDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
       .UseSnakeCaseNamingConvention());  // pacote EFCore.NamingConventions
```

`UseSnakeCaseNamingConvention()` converte automaticamente `JourneyContext` → `journey_contexts`, `CreatedAt` → `created_at`. Uma configuração, resolve todo o mapeamento de nomes.

---

## 2. Entity configurations

**Separe cada entidade em uma classe de configuração, não polua o DbContext.**

```csharp
// Data/Configurations/CustomerConfiguration.cs
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Cpf).HasMaxLength(11).IsRequired();
        builder.HasIndex(c => c.Cpf).IsUnique();
        builder.Property(c => c.FullName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("NOW()");
    }
}
```

Automaticamente carregada por `ApplyConfigurationsFromAssembly`.

---

## 3. Relacionamentos

**Configure explicitamente. Não confie em convention.**

```csharp
public class JourneyContextConfiguration : IEntityTypeConfiguration<JourneyContext>
{
    public void Configure(EntityTypeBuilder<JourneyContext> builder)
    {
        builder.HasOne(j => j.Customer)
            .WithMany(c => c.JourneyContexts)
            .HasForeignKey(j => j.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(j => j.Transitions)
            .WithOne(t => t.JourneyContext)
            .HasForeignKey(t => t.JourneyContextId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

## 4. Migrations com EF Core

```bash
# Criar
dotnet ef migrations add DescriptiveName

# Revisar SQL
dotnet ef migrations script

# Aplicar em dev
dotnet ef database update

# Reverter uma migration (só se ainda não foi para outro ambiente!)
dotnet ef migrations remove
```

**No `Program.cs`, aplicar migrations pendentes na startup (só em dev/staging):**

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        db.Database.Migrate();
    }
}
```

Em produção, aplicação de migrations deve ser um passo explícito do deploy, não automático.

---

## 5. Evitar N+1

Ver §16 da Parte I. Repetindo pela importância: **sempre use `Include`, `ThenInclude` ou projeção quando for iterar navegações.**

Habilite log de queries em dev para pegar N+1:

```json
"Logging": {
    "LogLevel": {
        "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
}
```

Se ao rodar uma operação você vir 20 queries iguais no console, é N+1.

---

## 6. AsNoTracking para leitura

```csharp
// Endpoint só lê, não vai atualizar — sem tracking
var journeys = await _db.JourneyContexts
    .AsNoTracking()
    .Where(j => j.Status == "open")
    .ToListAsync(ct);
```

`AsNoTracking` reduz uso de memória e CPU. Regra: **use por padrão em queries de leitura**; remova só se for atualizar a entidade lida.

---

## 7. Bulk operations

**EF Core não é bom para inserts/updates em massa** (ele gera uma round-trip por linha).

Para bulk (>1000 linhas):

- Pacote `EFCore.BulkExtensions` (`BulkInsert`, `BulkUpdate`).
- Ou `COPY` do Postgres via `Npgsql` direto.
- Ou SQL raw.

Para o CFE isso provavelmente não será necessário. Mas se um dia surgir um seed com 100k clientes, você lembra.

---

# Parte IV — Fluxo de trabalho

## 1. Git

**Branches:**
- `main` — sempre deployável.
- `feat/nome-da-feature` — novas funcionalidades.
- `fix/descricao-do-bug` — correções.
- `chore/refactor-x` — sem impacto funcional.

**Uma feature por branch.** Não misture refactor grande com feature nova.

**Rebase antes de merge quando possível** para manter histórico linear.

---

## 2. Commits

**Convenção: Conventional Commits.**

```
tipo(escopo): descrição curta no imperativo em inglês

Corpo opcional explicando o porquê.

Refs: #123
```

**Tipos:**

- `feat`: nova funcionalidade
- `fix`: correção de bug
- `chore`: tarefas de manutenção (deps, config)
- `docs`: documentação
- `refactor`: refactor sem mudança de comportamento
- `test`: adição/ajuste de testes
- `perf`: melhoria de performance
- `build`: build system, CI
- `style`: formatação (sem mudança de código)

**Exemplos bons:**

```
feat(identity): add resolve endpoint with auto-create
fix(context): return 410 when journey expired
chore(deps): bump ef core to 8.0.4
docs(readme): add setup section for postgres
refactor(context): extract transition recording to service
```

**Exemplos ruins:**

```
❌ update
❌ WIP
❌ fixes
❌ trabalhando ainda
```

**Regra do commit atômico:** um commit deve fazer uma coisa. Se você precisa de "e" na mensagem, provavelmente são dois commits.

---

## 3. Code review próprio (self-review)

Antes de fazer merge para main, revise seu próprio código como se fosse outra pessoa:

- Cada arquivo mudado — tem razão de estar aqui?
- Sobrou `Console.WriteLine`, `TODO`, código comentado?
- Nomes fazem sentido em 6 meses?
- Erro de negócio está claro no log?
- Migrations foram testadas?
- Segredo escapou pro commit?

Uma passada de 5 minutos evita muito retrabalho.

---

## 4. Testes manuais mínimos

Mesmo sem testes automatizados no MVP, garanta:

- **Swagger cobre tudo.** Bater cada endpoint pelo Swagger deve funcionar.
- **Caminho feliz + 2 erros por endpoint.** Payload inválido, ID inexistente, estado inválido.
- **Verifique no banco.** Depois de operação de escrita, cheque via `psql` ou pgAdmin se os dados ficaram como esperado.
- **Log fez sentido?** Se você olhar o log de uma operação real, dá pra reconstruir o que aconteceu?

Testes automatizados podem vir depois do MVP funcionar. Testes manuais estruturados são inegociáveis.

---

# Checklist de fim de dia

Ao final de cada dia de desenvolvimento, marque:

- [ ] Código compila sem warnings novos.
- [ ] Aplicação inicia e `/health` responde 200.
- [ ] Novos endpoints têm doc no Swagger e foram testados manualmente.
- [ ] Nenhum segredo entrou no commit (`git diff` antes do push).
- [ ] Migrations pendentes foram criadas para todas as mudanças de schema.
- [ ] Commits com mensagens claras e atômicas.
- [ ] README atualizado se algo mudou no setup.
- [ ] Nada de `TODO` bloqueante deixado sem registro.

---

**Fim do documento.**
