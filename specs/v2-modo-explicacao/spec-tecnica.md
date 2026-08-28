# Especificação Técnica — Claro Flow Engine (CFE)

**Projeto:** Claro Flow Engine (CFE) — Protótipo Funcional
**Time:** Horizon (FIAP 4SI / Challenge Claro 2026)
**Versão:** 1.1
**Público-alvo:** desenvolvedor(a) e/ou agente de IA responsável pela codificação
**Complementa:** Especificação Funcional (spec-funcional.md)
**Complementado por:** Especificação do Modo Explicação (spec-modo-explicacao.md), que detalha o backend do Explainer, o Painel de Orquestração e a integração com os canais

---

## 1. Stack tecnológico

A tabela abaixo consolida a stack, com componente, tecnologia e justificativa (formato solicitado nos feedbacks).

| Componente | Tecnologia | Justificativa |
|---|---|---|
| Linguagem backend | C# 12 | Domínio da equipe; forte tipagem; ecossistema maduro para APIs corporativas |
| Runtime backend | .NET 8 (LTS) | Versão LTS mais recente; suporte estendido; ganhos de performance em relação ao .NET 6 |
| Framework Web | ASP.NET Core Web API | Padrão de mercado para APIs REST em .NET; integração nativa com DI, middleware, Swagger |
| ORM | Entity Framework Core 8 | Produtividade no MVP; migrations versionadas; querying tipado |
| Banco de dados | PostgreSQL 16 | Open-source, suporte robusto a JSONB (útil para payload de jornada), amplamente adotado |
| Driver Postgres | Npgsql 8 | Driver oficial para .NET; alinhado com EF Core 8 |
| Documentação de API | Swagger / Swashbuckle | Nativo em ASP.NET Core; documentação e teste manual em uma interface só |
| Logging | Serilog | Logs estruturados em JSON; integração com sinks (console, arquivo, seq) |
| Front dos canais simulados | HTML5 + CSS3 + JavaScript (vanilla) | Sem build step; menor complexidade para o MVP; equivalência funcional ao React para o escopo |
| Servidor estático para front | Live Server (VS Code) ou `dotnet serve` ou `http-server` (npm) | Qualquer servidor estático simples; sem preferência forte |
| Containerização de dependência | Docker Compose | Sobe o PostgreSQL em um comando, sem instalar Postgres local |
| Controle de versão | Git + GitHub | Padrão da equipe |
| IDE | Visual Studio 2022 ou VS Code + C# Dev Kit | Ambos suportados |

---

## 2. Arquitetura de componentes

Recapitulando a arquitetura definida no Sprint 2, agora com foco na materialização técnica.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              CHANNELS (browsers)                          │
│   ┌──────────────────┐  ┌────────────────┐  ┌─────────────────────────┐  │
│   │  whatsapp-sim/   │  │  minha-claro-  │  │  attendant-panel/       │  │
│   │  (HTML/CSS/JS)   │  │  app/          │  │  (HTML/CSS/JS + polling)│  │
│   └────────┬─────────┘  └────────┬───────┘  └────────────┬────────────┘  │
│            │                     │                        │               │
└────────────┼─────────────────────┼────────────────────────┼───────────────┘
             │                     │                        │
             │            HTTPS / REST / JSON (X-Channel-Token header)
             │                     │                        │
             ▼                     ▼                        ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     ClaroFlowEngine.Api (ASP.NET Core)                    │
│  ┌───────────────────────────────────────────────────────────────────┐   │
│  │  Middleware: Serilog · CorrelationId · ExceptionHandler · Auth    │   │
│  └───────────────────────────────────────────────────────────────────┘   │
│                                                                           │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐             │
│  │ Identity       │  │  Context       │  │  Handoff       │             │
│  │ Controller +   │  │  Controller +  │  │  Controller +  │             │
│  │ Service        │  │  Service       │  │  Service       │             │
│  └───────┬────────┘  └────────┬───────┘  └────────┬───────┘             │
│          │                    │                    │                     │
│          └────────────────────┼────────────────────┘                     │
│                               │                                          │
│                    ┌──────────▼──────────┐                               │
│                    │   CfeDbContext      │  (EF Core)                    │
│                    └──────────┬──────────┘                               │
└───────────────────────────────┼──────────────────────────────────────────┘
                                │
                                ▼
                    ┌─────────────────────┐
                    │   PostgreSQL 16     │
                    │   (Docker Compose)  │
                    └─────────────────────┘
```

**Observações importantes:**

- Os três módulos (Identity, Context, Handoff) coexistem no **mesmo processo .NET** (monolito modular). Não há chamadas HTTP entre eles. Serviços de um módulo podem chamar serviços de outro via injeção de dependência.
- O `CfeDbContext` é único, mas cada módulo tem seus próprios repositórios/services e opera nas suas entidades de responsabilidade.
- Cada canal se comunica com a API por REST/JSON. Nenhum canal fala com outro canal diretamente.
- A partir da versão 1.1 desta spec, existe um **quarto módulo lógico: Explain** — responsável pelo modo explicação didático. Ele é ortogonal aos demais (não é chamado no fluxo de negócio; ao contrário, ele intercepta pontos-chave dos outros módulos via chamadas explícitas `PauseAsync`). Detalhes técnicos completos em `spec-modo-explicacao.md`, incluindo: modelo de dados adicional (`explain_sessions`, `explain_steps`), contratos de API (`/explain/*`), mecanismo de pausa com `TaskCompletionSource<bool>`, Server-Sent Events para atualização em tempo real, e integração com os canais existentes via header `X-Explain-Session-Id`.
- **Um quarto canal também surge:** o Painel de Orquestração, em `channels/orchestration-panel/`. Ele consome os endpoints do módulo Explain e não interage com Identity/Context/Handoff diretamente.

---

## 3. Estrutura do projeto

Estrutura recomendada de pastas e arquivos:

```
ClaroFlowEngine/
├── docker-compose.yml
├── .gitignore
├── README.md
├── docs/
│   ├── spec-funcional.md
│   └── spec-tecnica.md
├── src/
│   └── ClaroFlowEngine.Api/
│       ├── ClaroFlowEngine.Api.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json           # gitignored
│       ├── Modules/
│       │   ├── Identity/
│       │   │   ├── Controllers/IdentityController.cs
│       │   │   ├── Services/IIdentityService.cs
│       │   │   ├── Services/IdentityService.cs
│       │   │   └── Dtos/
│       │   │       ├── ResolveIdentityRequest.cs
│       │   │       └── ResolveIdentityResponse.cs
│       │   ├── Context/
│       │   │   ├── Controllers/ContextController.cs
│       │   │   ├── Services/IContextService.cs
│       │   │   ├── Services/ContextService.cs
│       │   │   └── Dtos/
│       │   ├── Handoff/
│       │   │   ├── Controllers/HandoffController.cs
│       │   │   ├── Services/IHandoffService.cs
│       │   │   ├── Services/HandoffService.cs
│       │   │   └── Dtos/
│       │   └── Explain/                                # módulo do modo explicação
│       │       ├── Controllers/ExplainController.cs
│       │       ├── Services/IExplainService.cs
│       │       ├── Services/ExplainService.cs         # Singleton, mantém TCS em ConcurrentDictionary
│       │       ├── Services/ISnapshotBroadcaster.cs
│       │       ├── Services/SseSnapshotBroadcaster.cs # Server-Sent Events
│       │       └── Dtos/
│       ├── Data/
│       │   ├── CfeDbContext.cs
│       │   ├── Entities/
│       │   │   ├── Customer.cs
│       │   │   ├── IdentityLink.cs
│       │   │   ├── Plan.cs
│       │   │   ├── CustomerPlan.cs
│       │   │   ├── JourneyContext.cs
│       │   │   ├── JourneyTransition.cs
│       │   │   └── HandoffToken.cs
│       │   ├── Configurations/                     # IEntityTypeConfiguration<T>
│       │   ├── Migrations/                         # geradas pelo EF
│       │   └── Seed/
│       │       └── DatabaseSeeder.cs
│       ├── Common/
│       │   ├── Middleware/
│       │   │   ├── CorrelationIdMiddleware.cs
│       │   │   ├── ChannelAuthMiddleware.cs
│       │   │   └── ExceptionHandlingMiddleware.cs
│       │   ├── Errors/
│       │   │   ├── ApiError.cs
│       │   │   └── ErrorCodes.cs
│       │   └── Extensions/
│       │       └── DateTimeExtensions.cs
│       └── Configuration/
│           ├── SwaggerConfig.cs
│           └── SerilogConfig.cs
└── channels/
    ├── whatsapp-sim/
    │   ├── index.html
    │   ├── styles.css
    │   ├── app.js                                  # máquina de estados + chamadas à API
    │   └── config.js                               # base URL da API, etc.
    ├── minha-claro-app/
    │   ├── index.html                              # rota /?token=xxx
    │   ├── styles.css
    │   └── app.js
    ├── attendant-panel/
    │   ├── index.html
    │   ├── styles.css
    │   └── app.js                                  # busca + polling
    └── orchestration-panel/                        # Painel do Modo Explicação
        ├── index.html
        ├── styles.css
        ├── app.js                                  # SSE + timeline + controle
        ├── diagram.js                              # renderização SVG do CFE
        └── config.js
```

**Notas sobre a estrutura:**

- Um único projeto .NET (`ClaroFlowEngine.Api`) contém tudo. Isso simplifica migrations, referências e execução. Separação em projetos `Core`/`Infrastructure` fica como refactor futuro.
- Cada módulo é uma pasta com Controllers, Services e DTOs. Isso preserva as fronteiras arquiteturais definidas no Sprint 2 sem exigir múltiplos projetos.
- A pasta `channels/` fica **fora** de `src/` porque não é código .NET; é servida separadamente (com qualquer servidor estático).

---

## 4. Modelagem de dados

### 4.1. Diagrama lógico (ER textual)

```
customers (1) ─── (N) identity_links
customers (1) ─── (N) customer_plans (N) ─── (1) plans
customers (1) ─── (N) journey_contexts
customers (1) ─── (N) invoices (1) ─── (N) invoice_items    -- ETAPA 2, Passo C
journey_contexts (1) ─── (N) journey_transitions
journey_contexts (1) ─── (N) handoff_tokens
```

### 4.2. DDL PostgreSQL

O SQL abaixo é o resultado esperado da migration inicial. O EF Core deve gerar algo equivalente.

```sql
-- Habilita função de UUID (execute uma vez no banco)
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

CREATE TABLE customers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cpf VARCHAR(11) UNIQUE NOT NULL,
    full_name VARCHAR(200) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE identity_links (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    channel VARCHAR(20) NOT NULL,         -- 'whatsapp', 'app', 'cpf', 'call'
    identifier VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ux_identity_links_channel_identifier UNIQUE (channel, identifier)
);

CREATE INDEX ix_identity_links_customer ON identity_links(customer_id);

CREATE TABLE plans (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) UNIQUE NOT NULL,     -- 'claro_15gb', 'claro_60gb'
    name VARCHAR(100) NOT NULL,           -- 'Claro 15GB'
    data_gb INT NOT NULL,
    monthly_price_cents INT NOT NULL,     -- guardar em centavos, evita float
    active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE customer_plans (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    plan_id UUID NOT NULL REFERENCES plans(id),
    active BOOLEAN NOT NULL DEFAULT TRUE,
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_customer_plans_customer ON customer_plans(customer_id, active);

CREATE TABLE journey_contexts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id),
    origin_channel VARCHAR(20) NOT NULL,
    intent VARCHAR(50) NOT NULL,          -- 'change_plan'
    current_step VARCHAR(50) NOT NULL,    -- 'identity_resolved', 'plan_selected', etc.
    payload JSONB NOT NULL DEFAULT '{}',
    status VARCHAR(20) NOT NULL,          -- 'open', 'concluded', 'expired', 'abandoned'
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    closed_at TIMESTAMPTZ
);

CREATE INDEX ix_journey_customer_status ON journey_contexts(customer_id, status);
CREATE INDEX ix_journey_open_updated ON journey_contexts(updated_at) WHERE status = 'open';

CREATE TABLE journey_transitions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    journey_context_id UUID NOT NULL REFERENCES journey_contexts(id) ON DELETE CASCADE,
    channel VARCHAR(20) NOT NULL,
    event_type VARCHAR(50) NOT NULL,      -- 'journey_started', 'identity_resolved',
                                          -- 'step_updated', 'deep_link_generated',
                                          -- 'journey_resumed', 'journey_closed',
                                          -- 'journey_expired', 'panel_accessed'
    description TEXT,
    metadata JSONB DEFAULT '{}',
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_transitions_journey_occurred ON journey_transitions(journey_context_id, occurred_at DESC);

CREATE TABLE handoff_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    journey_context_id UUID NOT NULL REFERENCES journey_contexts(id) ON DELETE CASCADE,
    token VARCHAR(100) UNIQUE NOT NULL,
    target_channel VARCHAR(20) NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    used_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_handoff_tokens_token ON handoff_tokens(token);

-- ETAPA 2, Passo C — intenção "contestação de cobrança indevida"
CREATE TABLE invoices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    reference_month DATE NOT NULL,          -- primeiro dia do mês da referência
    due_date DATE NOT NULL,
    total_cents INT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'issued', -- 'issued', 'paid', 'overdue', 'contested'
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_invoices_status CHECK (status IN ('issued', 'paid', 'overdue', 'contested')),
    CONSTRAINT ck_invoices_total_positive CHECK (total_cents > 0)
);

CREATE INDEX ix_invoices_customer_month ON invoices(customer_id, reference_month DESC);

CREATE TABLE invoice_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    invoice_id UUID NOT NULL REFERENCES invoices(id) ON DELETE CASCADE,
    description VARCHAR(200) NOT NULL,       -- 'Mensalidade Claro 60GB', 'Franquia adicional 2GB', 'ICMS'
    category VARCHAR(50) NOT NULL,           -- 'subscription', 'add_on', 'tax', 'fee'
    amount_cents INT NOT NULL,
    sequence INT NOT NULL,                   -- ordem de exibição
    CONSTRAINT ux_invoice_items_invoice_sequence UNIQUE (invoice_id, sequence)
);

CREATE INDEX ix_invoice_items_invoice ON invoice_items(invoice_id);
```

### 4.3. Seed obrigatório

```sql
-- planos
INSERT INTO plans (code, name, data_gb, monthly_price_cents) VALUES
    ('claro_15gb', 'Claro 15GB',  15,  4990),
    ('claro_30gb', 'Claro 30GB',  30,  5990),
    ('claro_60gb', 'Claro 60GB',  60,  8990),
    ('claro_100gb','Claro 100GB',100, 11990);

-- clientes de teste
INSERT INTO customers (cpf, full_name) VALUES
    ('11144477735', 'Ana Silva'),
    ('22255588846', 'Carlos Mendes'),
    ('33366699957', 'Mariana Souza');

-- links de identidade (associar telefones aos clientes do seed)
-- (usar subqueries para pegar os IDs)

-- planos ativos por cliente (Ana e Carlos têm plano; Mariana também)

-- faturas (ETAPA 2, Passo C): 3 por cliente (últimos 3 meses), 5 itens de linha cada.
-- A fatura mais recente de cada cliente inclui um item de valor estranho, para a demo de contestação.
```

O seeder em C# fará isso via EF Core (ver seção 8).

---

## 5. Contratos de API

Todos os endpoints são REST/JSON. Base URL local: `http://localhost:5000` (ou porta configurada).

### 5.1. Convenções gerais

- Content-Type: `application/json`
- Encoding: UTF-8
- Datas em ISO 8601 UTC (`"2026-07-29T14:30:00Z"`)
- IDs em UUID string
- Todo endpoint exceto `/health` e `/swagger` exige o header `X-Channel-Token`
- Respostas de erro seguem o padrão:

```json
{
  "error_code": "invalid_cpf",
  "message": "CPF inválido — verifique os dígitos digitados.",
  "details": { "field": "identifier" }
}
```

### 5.2. Módulo Identity

#### `POST /identity/resolve`

Resolve ou cria a identidade unificada para um par (canal, identificador).

**Request:**
```json
{
  "channel": "whatsapp",
  "identifier": "5511999998888",
  "cpf_hint": "11144477735",
  "full_name_hint": null
}
```

- `channel`: obrigatório. Valores: `whatsapp`, `app`, `cpf`, `call`.
- `identifier`: obrigatório. Formato depende do canal (ver Spec Funcional §6.2).
- `cpf_hint`: opcional. Usado quando o canal não é `cpf` mas o cliente informou o CPF na conversa. Se fornecido, o sistema tenta encontrar o cliente por CPF e vincular o canal atual a ele.
- `full_name_hint`: opcional. Necessário para criar novo cliente quando o CPF não está cadastrado.

**Response 200:**
```json
{
  "unified_customer_id": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
  "customer": {
    "id": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
    "full_name": "Ana Silva",
    "cpf": "11144477735"
  },
  "was_created": false,
  "resolved_link": { "channel": "whatsapp", "identifier": "5511999998888" }
}
```

**Response 400:** identificador em formato inválido.
**Response 404:** CPF não encontrado E `full_name_hint` não fornecido (não dá para criar).

#### `GET /identity/resolve?channel={c}&identifier={i}`

Versão idempotente somente para consulta (sem criação).

**Response 200:** igual acima.
**Response 404:** identidade não encontrada.

---

### 5.3. Módulo Context

#### `POST /context/open`

Abre uma nova jornada.

**Request:**
```json
{
  "customer_id": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
  "origin_channel": "whatsapp",
  "intent": "change_plan",
  "initial_step": "identity_resolved",
  "payload": {}
}
```

**Response 201:**
```json
{
  "id": "aaaa1111-...",
  "customer_id": "3f2504e0-...",
  "origin_channel": "whatsapp",
  "intent": "change_plan",
  "current_step": "identity_resolved",
  "payload": {},
  "status": "open",
  "created_at": "2026-07-29T14:30:00Z",
  "updated_at": "2026-07-29T14:30:00Z"
}
```

**Response 200 (idempotente):** se já existe jornada `open` para esse cliente com mesma intent, retorna a existente sem duplicar. Registrar transição `journey_reopen_attempted`.

#### `PATCH /context/{id}`

Atualiza etapa e/ou payload.

**Request:**
```json
{
  "current_step": "plan_selected",
  "payload_merge": {
    "selected_plan_code": "claro_60gb"
  }
}
```

- `payload_merge` é aplicado como merge no JSONB existente (não substitui o objeto inteiro).

**Response 200:** jornada atualizada.
**Response 404:** jornada não existe.
**Response 409:** jornada não está `open` (retorna estado atual).
**Response 410:** jornada expirou (verificação reativa) — antes de responder, marca como `expired`.

#### `GET /context/{id}`

Retorna estado atual da jornada.

**Response 200:**
```json
{
  "id": "aaaa1111-...",
  "customer_id": "3f2504e0-...",
  "customer": { "full_name": "Ana Silva", "cpf": "11144477735" },
  "origin_channel": "whatsapp",
  "intent": "change_plan",
  "current_step": "plan_selected",
  "payload": {
    "selected_plan_code": "claro_60gb",
    "current_plan_code": "claro_15gb"
  },
  "status": "open",
  "created_at": "...",
  "updated_at": "..."
}
```

#### `GET /context/customer/{customerId}`

Retorna a jornada **aberta** do cliente (ou 404 se não houver).

Query params opcionais:
- `include_history=true` — se `true`, retorna também as últimas jornadas fechadas em `recent_journeys`.
- `history_limit` (int, padrão `5`, ETAPA 2 Passo B) — limita quantas jornadas fechadas vêm em `recent_journeys`.

O `customer` embutido na jornada (quando há jornada ativa) inclui, desde a ETAPA 2 (Passo B): `customer_since` (data de cadastro), `preferred_channel` (canal de origem com mais jornadas do cliente, empate resolvido pelo mais recente) e `journey_counts` (`{ total, concluded, abandoned, expired }`). Esses agregados são calculados a cada chamada (dataset pequeno no protótipo), não incorporados a um endpoint novo — decisão do agente para evitar multiplicar endpoints no MVP.

#### `GET /context/{id}/transitions`

Retorna o histórico de transições da jornada, ordenado do mais recente para o mais antigo.

**Response 200:**
```json
{
  "journey_context_id": "aaaa1111-...",
  "transitions": [
    {
      "id": "...",
      "channel": "app",
      "event_type": "journey_resumed",
      "description": "Cliente abriu o deep link. App carregou tela de confirmação.",
      "metadata": {},
      "occurred_at": "2026-07-29T14:35:12Z"
    },
    {
      "id": "...",
      "channel": "whatsapp",
      "event_type": "deep_link_generated",
      "description": "Deep link gerado com validade de 30 min.",
      "metadata": { "token_expires_at": "2026-07-29T15:04:12Z" },
      "occurred_at": "2026-07-29T14:34:12Z"
    }
  ]
}
```

#### `POST /context/{id}/close`

Encerra uma jornada.

**Request:**
```json
{
  "outcome": "concluded",
  "channel": "app",
  "reason": null
}
```

- `outcome`: obrigatório. Valores: `concluded`, `abandoned`.
- `channel`: canal onde o encerramento aconteceu.
- `reason`: opcional, texto livre.

**Response 200:** jornada fechada.
**Response 409:** jornada já estava fechada.

---

### 5.4. Módulo Handoff

#### `POST /handoff/generate`

Gera um deep link com token.

**Request:**
```json
{
  "journey_context_id": "aaaa1111-...",
  "target_channel": "app"
}
```

**Response 201:**
```json
{
  "token": "b0c5f4e2-a1b3-4d7e-8f2a-9c1d3e5f7a90",
  "target_channel": "app",
  "deep_link_url": "http://localhost:5173/?token=b0c5f4e2-a1b3-4d7e-8f2a-9c1d3e5f7a90",
  "expires_at": "2026-07-29T15:04:12Z"
}
```

O `deep_link_url` é montado a partir de uma configuração `AppSimBaseUrl` do `appsettings`.

#### `GET /context/resolve?token={token}`

Endpoint usado pelo canal de destino (App simulado) para recuperar contexto via token.

**Response 200:**
```json
{
  "unified_customer_id": "3f2504e0-...",
  "journey_context": {
    "id": "aaaa1111-...",
    "intent": "change_plan",
    "current_step": "plan_selected",
    "payload": { "selected_plan_code": "claro_60gb", "current_plan_code": "claro_15gb" },
    "status": "open"
  },
  "customer": { "full_name": "Ana Silva", "cpf": "11144477735" },
  "plan_details": {
    "current_plan": { "code": "claro_15gb", "name": "Claro 15GB", "monthly_price_cents": 4990 },
    "selected_plan": { "code": "claro_60gb", "name": "Claro 60GB", "monthly_price_cents": 8990 }
  }
}
```

**Response 404:** token não existe.
**Response 410:** token expirado, já usado, ou jornada expirada/fechada. Nesse caso o Body deve indicar qual foi o motivo:
```json
{ "error_code": "token_expired", "message": "..." }
{ "error_code": "token_already_used", "message": "..." }
{ "error_code": "journey_expired", "message": "..." }
{ "error_code": "journey_closed", "message": "..." }
```

Ao resolver com sucesso, o sistema:
- Marca `used_at = NOW()`.
- Cria `identity_link` para o canal `app` (se ainda não existir).
- Registra transição `journey_resumed`.

---

### 5.5. Endpoints auxiliares

#### `GET /health`

Health check. Retorna 200 com:
```json
{ "status": "healthy", "checks": { "db": "healthy" } }
```

#### `GET /plans`

Lista os planos ativos (útil para o chat mostrar as opções).

**Response 200:**
```json
{
  "plans": [
    { "code": "claro_15gb", "name": "Claro 15GB", "data_gb": 15, "monthly_price_cents": 4990 },
    { "code": "claro_30gb", "name": "Claro 30GB", "data_gb": 30, "monthly_price_cents": 5990 },
    { "code": "claro_60gb", "name": "Claro 60GB", "data_gb": 60, "monthly_price_cents": 8990 },
    { "code": "claro_100gb","name": "Claro 100GB","data_gb":100,"monthly_price_cents":11990 }
  ]
}
```

---

### 5.6. Módulo Invoices (ETAPA 2, Passo C)

#### `GET /invoices/customer/{customerId}?limit=N`

Lista as últimas `N` (padrão 3) faturas do cliente, ordenadas por `reference_month` DESC.

**Response 200:**
```json
{
  "customer_id": "...",
  "invoices": [
    {
      "id": "...",
      "reference_month": "2026-10-01",
      "reference_label": "Outubro/2026",
      "due_date": "2026-10-15",
      "total_cents": 18990,
      "status": "issued"
    }
  ]
}
```

**Response 404:** cliente não encontrado.

#### `GET /invoices/{invoiceId}`

Detalhe de uma fatura, incluindo os itens de linha.

**Response 200:**
```json
{
  "id": "...",
  "customer_id": "...",
  "reference_month": "2026-10-01",
  "reference_label": "Outubro/2026",
  "due_date": "2026-10-15",
  "total_cents": 18990,
  "status": "issued",
  "items": [
    { "id": "...", "sequence": 1, "description": "Mensalidade Claro 60GB", "category": "subscription", "amount_cents": 8990 },
    { "id": "...", "sequence": 2, "description": "Franquia adicional 2GB", "category": "add_on", "amount_cents": 1990 },
    { "id": "...", "sequence": 3, "description": "ICMS", "category": "tax", "amount_cents": 3600 },
    { "id": "...", "sequence": 4, "description": "PIS/COFINS", "category": "tax", "amount_cents": 1200 },
    { "id": "...", "sequence": 5, "description": "Taxa de conveniência", "category": "fee", "amount_cents": 3210 }
  ]
}
```

**Response 404:** fatura não encontrada.

Ambos endpoints exigem `X-Channel-Token`, como os demais. Nenhuma validação de propriedade (cliente ↔ fatura) é aplicada — ver spec funcional §6.9 para a justificativa (mesma simplificação de autenticação do resto do protótipo).

#### Enriquecimento de `GET /context/resolve`

Quando `journey_context.intent === 'dispute_charge'` e `payload.invoice_id` existir, o response de `GET /context/resolve` (§5.4) ganha um campo adicional `invoice_details`, com o mesmo formato do detalhe acima. **Decisão do agente:** aditivo, incorporado ao endpoint existente em vez de o App fazer uma segunda chamada só para buscar a fatura — análogo ao `plan_details` já existente para a troca de plano.

---

## 6. Máquinas de estado — implementação

### 6.1. Journey Status (no banco)

Enum modelado como string:
```csharp
public static class JourneyStatus
{
    public const string Open       = "open";
    public const string Concluded  = "concluded";
    public const string Expired    = "expired";
    public const string Abandoned  = "abandoned";
}
```

Transições permitidas (validadas no `ContextService`):
- `open → concluded/abandoned` via `POST /context/{id}/close`.
- `open → expired` automaticamente na regra reativa.

Qualquer outra transição resulta em 409.

### 6.2. Bot Conversation State (chat simulado)

Como o chat simulado é HTML/JS, o estado da conversa pode ser mantido em memória do navegador (localStorage ou objeto JS). Não é necessário persistir estado do bot no servidor — o estado da **jornada** já está persistido no CFE, e essa é a única coisa que importa.

Estados no cliente (JS):

```js
const BOT_STATES = {
  GREETING: 'greeting',
  AWAITING_INTENT: 'awaiting_intent',
  AWAITING_CPF: 'awaiting_cpf',
  AWAITING_NAME: 'awaiting_name',           // se cliente novo
  IDENTITY_RESOLVED: 'identity_resolved',
  AWAITING_PLAN_CHOICE: 'awaiting_plan_choice',
  LINK_GENERATED: 'link_generated',
  COMPLETED: 'completed',
  ERROR: 'error'
};
```

Regras:
- Cada input do usuário é despachado para uma função `handleMessage(state, message)` que retorna `{ nextState, botReply, apiCalls }`.
- `apiCalls` é uma lista de chamadas a executar (ex: `/identity/resolve`, `/context/open`, `/handoff/generate`).
- As chamadas são executadas em ordem, e falhas devem levar o bot ao estado `ERROR` com mensagem apropriada.

---

## 7. Frontends simulados

Todos os três canais são páginas HTML autônomas.

### 7.1. Servindo os canais

Recomendação: cada canal servido em uma porta separada para simular canais distintos.

Opções:
- **Live Server (VS Code):** clique-direito → "Open with Live Server" em cada `index.html`.
- **`http-server` (npm):**
  ```
  npx http-server channels/whatsapp-sim -p 5171
  npx http-server channels/minha-claro-app -p 5173
  npx http-server channels/attendant-panel -p 5175
  ```
- **`dotnet serve`:** alternativa em .NET.

O `appsettings.json` do backend deve conter as URLs:
```json
"Channels": {
  "WhatsappSimBaseUrl": "http://localhost:5171",
  "AppSimBaseUrl": "http://localhost:5173",
  "AttendantPanelBaseUrl": "http://localhost:5175"
}
```

E o backend usa `AppSimBaseUrl` para montar `deep_link_url`.

**Nova tela do App (ETAPA 2, Passo C):** `channels/minha-claro-app/index.html` ganhou uma segunda seção de conteúdo (`#screen-dispute-confirmation`), renderizada em vez da tela de confirmação de troca de plano quando `journey_context.intent === 'dispute_charge'`. Descrição funcional completa em spec-funcional §8.2; contrato de dados (`invoice_details`) em spec-tecnica §5.6.

### 7.2. Comunicação com a API

Todos os canais fazem `fetch` para `http://localhost:5000` (URL da API, configurável em `channels/*/config.js`).

Cada canal envia um header `X-Channel-Token` mockado:
- `whatsapp-sim` → `X-Channel-Token: fake-whatsapp-token`
- `minha-claro-app` → `X-Channel-Token: fake-app-token`
- `attendant-panel` → `X-Channel-Token: fake-panel-token`

O middleware do backend valida que o header existe e está em uma allowlist configurada. **Não valida assinatura, expiração, nada** — é mock explícito para documentar a intenção arquitetural.

### 7.3. CORS

A API deve permitir CORS das três origens locais dos canais:

```csharp
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:5171", "http://localhost:5173", "http://localhost:5175")
    .AllowAnyHeader()
    .AllowAnyMethod()));
```

### 7.4. Polling no painel

O painel implementa polling simples com `setInterval`:

```js
async function refreshPanel() {
  const contextRes = await fetch(`${API}/context/${journeyId}`, { headers: { 'X-Channel-Token': 'fake-panel-token' } });
  const transitionsRes = await fetch(`${API}/context/${journeyId}/transitions`, { headers: { 'X-Channel-Token': 'fake-panel-token' } });
  // rerender UI
}

setInterval(refreshPanel, 4000); // a cada 4 segundos
```

Cuidados:
- Cancelar o interval quando o usuário sair da página ou trocar de cliente.
- Se a jornada mudar para status final, parar o polling e exibir status final.

---

## 8. Padrões de código

### 8.1. Nomenclatura

- **Código, classes, métodos, variáveis, colunas de banco, endpoints:** **inglês**.
- **Comentários no código, mensagens de erro visíveis ao usuário, documentação:** **português**.
- **Nomes de branches Git:** inglês (`feat/identity-resolve`, `fix/token-expiration`).
- **Mensagens de commit:** inglês, convenção Conventional Commits (`feat:`, `fix:`, `chore:`, `docs:`, `refactor:`).

### 8.2. Camadas

Cada módulo segue o padrão:

```
Controller ── (injeta) ──> Service ── (usa) ──> DbContext (EF Core)
```

- **Controller:** só recebe requisição, valida shape via DTO, chama Service, monta HTTP response. Nenhuma regra de negócio.
- **Service:** contém regras de negócio, validações, orquestração entre repositórios/DbContext.
- **DbContext:** acesso a dados. Sem repositório dedicado nesta versão (EF é o repositório). Se o time preferir, extrair um repository por módulo é aceitável — mas não obrigatório para o MVP.

### 8.3. DTOs vs Entities

- **Entities** ficam em `Data/Entities/` e refletem exatamente o schema do banco.
- **DTOs** ficam em cada módulo (`Modules/X/Dtos/`) e servem para request/response da API.
- **Nunca retorne Entity direto no JSON.** Sempre mapeie para DTO. Isso evita vazamento de campos internos e loops de serialização.

Mapeamento manual é aceitável para o MVP. AutoMapper é overkill nesta escala.

### 8.4. Tratamento de erros

Middleware global (`ExceptionHandlingMiddleware`) captura exceções e converte para JSON padronizado. Definir exceções de domínio:

```csharp
public class NotFoundException : Exception { public string ErrorCode { get; } }
public class ConflictException : Exception { ... }
public class GoneException : Exception { ... }
public class ValidationException : Exception { ... }
```

O middleware mapeia:
- `NotFoundException` → 404 + `error_code`
- `ConflictException` → 409
- `GoneException` → 410
- `ValidationException` → 400
- Qualquer outra → 500 (log completo, resposta genérica ao cliente)

### 8.5. Logging estruturado

Serilog configurado em `Program.cs` com sinks para console (JSON estruturado) e arquivo (`logs/cfe-YYYYMMDD.log`).

Contexto obrigatório em todo log dentro de uma operação:
- `correlation_id` — GUID gerado por request pelo middleware.
- `journey_context_id` — quando aplicável.
- `channel` — canal que originou a requisição.

Exemplo:
```csharp
_logger.LogInformation(
    "Journey opened for customer {CustomerId} on {Channel} with intent {Intent}",
    customerId, channel, intent);
```

### 8.6. Convenções de request/response

- Camel_case ou snake_case? **snake_case no JSON** (mais legível, e alinhado com o que APIs REST costumam usar). Configurar Newtonsoft.Json ou System.Text.Json com `SnakeCaseNamingPolicy`.
- Timestamps sempre em ISO 8601 UTC.
- UUIDs sempre como string.

---

## 9. Configuração do ambiente

O projeto suporta **dois modos de execução**, para propósitos diferentes:

| | **Modo desenvolvimento** | **Modo full (Docker)** |
|---|---|---|
| Quando usar | Dia a dia de desenvolvimento — hot reload, debug, iteração rápida | Demo/teste de ponta a ponta, validar que "do zero" funciona, sem instalar .NET/node localmente |
| O que sobe | Só o Postgres via Docker; API via `dotnet run`; canais via `http-server` (3 processos separados) | Postgres **e** API via Docker Compose; API também serve os canais estáticos em `/channels/*` |
| Arquivo | `docker-compose.yml` (só banco) | `docker-compose.full.yml` (banco + API) |
| Portas dos canais | 5171 / 5173 / 5175 (uma por canal) | Todas sob `http://localhost:5104/channels/<nome-do-canal>` (mesma origem da API) |

Os dois arquivos de compose são independentes — cada um sobe seu próprio container de Postgres (nomes e volumes distintos), então **não é necessário nem recomendado rodar os dois ao mesmo tempo** sem ajustar portas.

### 9.1. Pré-requisitos

**Modo desenvolvimento:**
- .NET SDK (8 LTS conforme documentado originalmente; o protótipo em implementação usa .NET 10, também LTS — decisão registrada nos relatórios de fase, sem impacto de contrato)
- Docker + Docker Compose
- Git
- Editor à escolha (VS Code recomendado + C# Dev Kit)
- Um servidor estático leve (Live Server, http-server, dotnet serve) — só para os canais

**Modo full:**
- Docker + Docker Compose — **nada mais**. Não precisa de .NET SDK nem Node instalados na máquina; tudo roda dentro dos containers.

### 9.2. Modo desenvolvimento

#### 9.2.1. docker-compose.yml (só o banco)

```yaml
services:
  postgres:
    image: postgres:16-alpine
    container_name: cfe-postgres
    environment:
      POSTGRES_USER: cfe
      POSTGRES_PASSWORD: cfe_local_pwd
      POSTGRES_DB: cfe
    ports:
      - "5432:5432"
    volumes:
      - cfe_pgdata:/var/lib/postgresql/data
volumes:
  cfe_pgdata:
```

> Nota de implementação: no ambiente onde o protótipo foi construído, a porta 5432 do host já estava ocupada por uma instância nativa do PostgreSQL do Windows, então o `docker-compose.yml` real do repositório mapeia `5433:5432` no host (a porta interna do container continua 5432). Ajuste conforme o seu ambiente.

#### 9.2.2. appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=cfe;Username=cfe;Password=cfe_local_pwd"
  },
  "Channels": {
    "WhatsappSimBaseUrl": "http://localhost:5171",
    "AppSimBaseUrl": "http://localhost:5173",
    "AttendantPanelBaseUrl": "http://localhost:5175"
  },
  "Cfe": {
    "HandoffTokenTtlMinutes": 30,
    "JourneyInactivityTtlHours": 24,
    "AllowedChannelTokens": [
      "fake-whatsapp-token",
      "fake-app-token",
      "fake-panel-token"
    ]
  },
  "Serilog": {
    "MinimumLevel": { "Default": "Information" }
  }
}
```

Este arquivo vai no `.gitignore`. O `appsettings.json` (versionado) tem valores placeholder.

#### 9.2.3. Comandos essenciais

```bash
# Subir postgres
docker compose up -d

# Criar migration inicial
cd src/ClaroFlowEngine.Api
dotnet ef migrations add InitialCreate

# Aplicar migrations (automatiza no Program.cs também)
dotnet ef database update

# Rodar API
dotnet run

# Rodar canais (em três terminais separados)
npx http-server ../../channels/whatsapp-sim -p 5171 -c-1
npx http-server ../../channels/minha-claro-app -p 5173 -c-1
npx http-server ../../channels/attendant-panel -p 5175 -c-1
```

Neste modo, a API também serve `/channels/*` automaticamente se a pasta `channels/` existir no caminho relativo configurado (`StaticFiles:ChannelsPath`, padrão `../../channels` a partir de `src/ClaroFlowEngine.Api/`) — é opcional usar isso em dev, já que os `http-server` separados cobrem o mesmo papel com hot-reload melhor por canal.

### 9.3. Modo full (Docker Compose completo)

Sobe Postgres **e** API juntos, com a API compilada dentro de uma imagem Docker (multi-stage: SDK para build, ASP.NET Core Runtime para execução) que também empacota a pasta `channels/` e a serve em `/channels/*`.

#### 9.3.1. Dockerfile (`src/ClaroFlowEngine.Api/Dockerfile`)

Multi-stage: o estágio `build` usa a imagem `sdk` (mais pesada, com compilador) só para gerar o publish; o estágio final usa a imagem `aspnet` (runtime, bem mais leve) e copia apenas o resultado do publish + a pasta `channels/`. O build precisa rodar com **contexto na raiz do repositório** (não em `src/ClaroFlowEngine.Api/`), porque o `COPY channels/` precisa enxergar essa pasta:

```bash
docker build -f src/ClaroFlowEngine.Api/Dockerfile -t claro-flow-engine-api .
```

#### 9.3.2. docker-compose.full.yml

```yaml
services:
  postgres:
    image: postgres:16-alpine
    container_name: cfe-postgres-full
    environment:
      POSTGRES_USER: cfe
      POSTGRES_PASSWORD: cfe_local_pwd
      POSTGRES_DB: cfe
    ports:
      - "5434:5432"
    volumes:
      - cfe_pgdata_full:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U cfe -d cfe"]
      interval: 5s
      timeout: 5s
      retries: 10

  api:
    build:
      context: .
      dockerfile: src/ClaroFlowEngine.Api/Dockerfile
    container_name: cfe-api-full
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Staging
      ConnectionStrings__Postgres: "Host=postgres;Port=5432;Database=cfe;Username=cfe;Password=cfe_local_pwd"
      Channels__WhatsappSimBaseUrl: "http://localhost:5104/channels/whatsapp-sim"
      Channels__AppSimBaseUrl: "http://localhost:5104/channels/minha-claro-app"
      Channels__AttendantPanelBaseUrl: "http://localhost:5104/channels/attendant-panel"
      Cfe__HandoffTokenTtlMinutes: "30"
      Cfe__JourneyInactivityTtlHours: "24"
      Cfe__AllowedChannelTokens__0: "fake-whatsapp-token"
      Cfe__AllowedChannelTokens__1: "fake-app-token"
      Cfe__AllowedChannelTokens__2: "fake-panel-token"
    ports:
      - "5104:8080"

volumes:
  cfe_pgdata_full:
```

Pontos relevantes:

- **`depends_on` com `condition: service_healthy`**: o container da API só inicia depois que o Postgres responde `pg_isready` — evita a corrida clássica de "API sobe antes do banco estar pronto para aceitar conexões".
- **`ASPNETCORE_ENVIRONMENT=Staging`**: a imagem, por padrão (definido no próprio `Dockerfile`), sobe como `Production` — nesse ambiente o `Program.cs` **não** aplica migration/seed automaticamente (conforme boa prática de não automatizar isso em produção). O compose sobrescreve para `Staging`, que já é um gatilho existente no código para aplicar migration + seed automaticamente, necessário aqui porque não há um passo de deploy manual separado neste modo local.
- **Host `postgres` na connection string**: dentro da rede interna do Compose, os serviços se resolvem pelo nome do serviço (DNS interno), não por `localhost`. A porta usada é a porta *interna* do container (`5432`), independente da porta publicada no host (`5434`).
- **Portas diferentes do `docker-compose.yml`** (Postgres em `5434` em vez de `5433`) para permitir rodar os dois stacks lado a lado sem conflito, se necessário.
- **`Channels:*BaseUrl` apontando para `/channels/<nome>`**: como a própria API serve os três canais sob o mesmo host/porta neste modo, os deep links gerados pelo Handoff (`POST /handoff/generate`) apontam para esses caminhos, não para portas separadas como no modo desenvolvimento.

#### 9.3.3. Comandos essenciais

```bash
# Build + subida da stack completa
docker compose -f docker-compose.full.yml up --build

# Em background
docker compose -f docker-compose.full.yml up --build -d

# Derrubar (mantendo o volume do banco)
docker compose -f docker-compose.full.yml down

# Derrubar e apagar o volume do banco também
docker compose -f docker-compose.full.yml down -v
```

Depois de subir, a API responde em `http://localhost:5104` (Swagger em `/swagger`, health check em `/health`) e os canais ficam em `http://localhost:5104/channels/whatsapp-sim/`, `/channels/minha-claro-app/` e `/channels/attendant-panel/` (os dois últimos ainda não implementados nas fases iniciais do protótipo).

### 9.4. Seed automático na inicialização

No `Program.cs`, após `app.Build()`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        db.Database.Migrate();
        await DatabaseSeeder.SeedAsync(db);
    }
}
```

O seeder cria planos e clientes de teste se ainda não existirem (idempotente). Roda em `Development` (modo desenvolvimento local) e `Staging` (modo full via Docker) — deliberadamente **não** roda em `Production`, conforme a boa prática de migrations serem um passo explícito de deploy nesse ambiente.

---

## 10. Observabilidade

### 10.1. Serilog

Configuração em `Program.cs`:

```csharp
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
    .WriteTo.File(
        formatter: new Serilog.Formatting.Json.JsonFormatter(),
        path: "logs/cfe-.log",
        rollingInterval: RollingInterval.Day));
```

### 10.2. Correlation ID

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

### 10.3. Health checks

`Program.cs`:
```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!);

app.MapHealthChecks("/health", new HealthCheckOptions {
    ResponseWriter = async (ctx, report) => {
        ctx.Response.ContentType = "application/json";
        var payload = new {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => e.Value.Status.ToString().ToLowerInvariant())
        };
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});
```

---

## 11. Segurança (mock explícito)

### 11.1. Autenticação de canal (`X-Channel-Token`)

Middleware simples:

```csharp
public class ChannelAuthMiddleware
{
    private readonly HashSet<string> _allowedTokens;

    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        if (IsPublicRoute(ctx.Request.Path)) { await next(ctx); return; }

        var token = ctx.Request.Headers["X-Channel-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(token) || !_allowedTokens.Contains(token))
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(new {
                    error_code = "invalid_channel_token",
                    message = "Token de canal ausente ou não autorizado."
                }));
            return;
        }
        await next(ctx);
    }

    private static bool IsPublicRoute(PathString path) =>
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/swagger") ||
        path.StartsWithSegments("/plans");
}
```

**Deixar comentário no código:**
```csharp
// MOCK — em produção, cada canal teria um JWT/serviço de identidade próprio.
// Este middleware simula a intenção arquitetural sem custo de setup de auth real.
```

### 11.2. CORS

Configurar apenas para as origens locais dos canais (§7.3).

### 11.3. HTTPS

Para o local, HTTP está ok. Em produção real seria HTTPS obrigatório — deixar comentário/nota no README.

---

## 12. Boas práticas técnicas

### 12.1. Migrations sempre versionadas

Toda mudança de schema é uma migration. Nunca alterar banco na mão em produção. No MVP, se precisar fazer ajuste rápido no desenvolvimento, é aceitável dropar e recriar, mas registrar depois em migration.

### 12.2. Sem segredos versionados

`.gitignore` deve incluir:
- `appsettings.Development.json`
- `appsettings.Production.json`
- `logs/`
- `bin/`, `obj/`

### 12.3. Idempotência em endpoints de criação

- `POST /identity/resolve` — se o link já existe, retorna; não duplica.
- `POST /context/open` — se já existe jornada `open` para o mesmo cliente + intent, retorna a existente.
- `POST /handoff/generate` — pode duplicar (é aceitável ter múltiplos tokens ativos para a mesma jornada); mas todos expiram em 30 min.

### 12.4. Verificação de expiração no ponto de leitura

Em vez de agendar um job para expirar jornadas, todo endpoint que lê uma jornada `open` executa antes:

```csharp
if (journey.Status == JourneyStatus.Open &&
    DateTime.UtcNow - journey.UpdatedAt > TimeSpan.FromHours(_cfeOptions.JourneyInactivityTtlHours))
{
    journey.Status = JourneyStatus.Expired;
    journey.ClosedAt = DateTime.UtcNow;
    await _transitionService.RecordAsync(journey.Id, "system", "journey_expired", "Jornada expirada por inatividade.");
    await _db.SaveChangesAsync();
}
```

Isso é executado dentro de uma transação para evitar corridas.

### 12.5. Transações onde importam

Operações que mudam múltiplas tabelas devem estar em `IDbContextTransaction`:
- Abertura de jornada + registro de transição.
- Fechamento de jornada + registro de transição.
- Resolução de token: marcar `used_at` + criar identity_link + registrar transição.

### 12.6. Registro de transição centralizado

Criar `ITransitionRecorder` injetado nos services:

```csharp
public interface ITransitionRecorder
{
    Task RecordAsync(Guid journeyId, string channel, string eventType,
                     string? description = null, object? metadata = null);
}
```

Toda mudança de estado passa por ele. Isso garante consistência do histórico e simplifica auditoria.

### 12.7. Nunca deixe hardcoded

TTLs, base URLs, tokens permitidos, tudo em `appsettings`. Injeção via `IOptions<CfeOptions>`.

### 12.8. Teste manual estruturado

Antes de considerar um endpoint "pronto":
- Testar caminho feliz via Swagger.
- Testar 3 caminhos de erro (payload inválido, id inexistente, estado inválido).
- Verificar no banco que os dados ficaram como esperado.
- Verificar no log que as mensagens fazem sentido.

### 12.9. Refactor no momento certo

Nos primeiros 3 dias, priorize funcionar. No dia 5, faça uma passada rápida de refactor onde tiver visto acumular dívida técnica. Não refatore no meio de implementar uma feature nova.

### 12.10. Commits ao final de cada bloco funcional

Modelo:
- `chore(setup): initial solution and docker-compose`
- `feat(db): initial migration with seven tables`
- `feat(identity): resolve endpoint with auto-create`
- `feat(context): open, update, get, close endpoints`
- `feat(handoff): token generation and resolution`
- `feat(channels): whatsapp-sim page with state machine`
- `feat(channels): app-sim with token resolution`
- `feat(channels): attendant panel with polling`
- `feat(observability): serilog structured logs`
- `chore(docs): readme with demo script`

---

## 13. Roteiro de setup passo a passo

### Passo 1 — Repositório

```bash
mkdir ClaroFlowEngine && cd ClaroFlowEngine
git init
```

Criar `.gitignore` (usar template padrão do .NET + os itens do §12.2).

### Passo 2 — Docker Compose

Criar `docker-compose.yml` (ver §9.2) e:
```bash
docker compose up -d
docker exec -it cfe-postgres psql -U cfe -d cfe -c 'CREATE EXTENSION IF NOT EXISTS pgcrypto;'
```

### Passo 3 — Projeto .NET

```bash
mkdir -p src && cd src
dotnet new webapi -n ClaroFlowEngine.Api --use-controllers
cd ClaroFlowEngine.Api

# Instalar pacotes
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Formatting.Compact
dotnet add package AspNetCore.HealthChecks.NpgSql
dotnet add package Swashbuckle.AspNetCore
```

Instalar tool global:
```bash
dotnet tool install --global dotnet-ef
```

### Passo 4 — Estrutura de pastas

Criar `Modules/`, `Data/`, `Common/`, `Configuration/` (ver §3).

### Passo 5 — DbContext e Entities

Criar entidades e `CfeDbContext`. Rodar:
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Verificar no banco que as tabelas foram criadas.

### Passo 6 — Seed

Implementar `DatabaseSeeder.SeedAsync` e chamar no `Program.cs`. Rodar e verificar no banco que planos e clientes existem.

### Passo 7 — Identity Module

Implementar `IdentityService`, `IdentityController`, DTOs. Testar via Swagger.

### Passo 8 — Context Module

Idem. Testar via Swagger (fluxo open → patch → get → close).

### Passo 9 — Handoff Module

Idem. Testar via Swagger o fluxo generate → resolve.

### Passo 10 — Canais

Criar `channels/whatsapp-sim/`, `channels/minha-claro-app/`, `channels/attendant-panel/`. Servir cada um em uma porta.

### Passo 11 — Observabilidade

Configurar Serilog, correlation id, health check.

### Passo 12 — README

Documentar como rodar. Registrar comandos, URLs, roteiro sugerido de demo.

---

## 14. Definition of Done técnica

O protótipo está tecnicamente pronto quando:

- [ ] `docker compose up -d` sobe o postgres corretamente.
- [ ] `dotnet run` na API inicia sem erros, aplica migrations, executa seed.
- [ ] Swagger acessível em `http://localhost:5000/swagger` com todos os endpoints documentados.
- [ ] `/health` retorna 200 com `db: healthy`.
- [ ] Todos os endpoints da §5 respondem conforme os contratos, incluindo os códigos de erro.
- [ ] Todo endpoint de escrita bem-sucedido cria uma linha em `journey_transitions`.
- [ ] Header `X-Channel-Token` é validado; ausência ou valor inválido resulta em 401.
- [ ] CORS liberado apenas para as origens configuradas dos canais.
- [ ] Chat simulado, App simulado e Painel do atendente funcionam nas suas respectivas portas.
- [ ] Fluxo ponta a ponta do chat até a confirmação no App funciona sem erros no console do navegador.
- [ ] Polling do painel atualiza automaticamente sem recarregar a página.
- [ ] Token expira em 30 min (testável ajustando `HandoffTokenTtlMinutes` para 1 min temporariamente).
- [ ] Jornada expira em 24h de inatividade (testável via `UPDATE journey_contexts SET updated_at = NOW() - INTERVAL '25 hours' WHERE id = ...`).
- [ ] Logs no console saem em JSON estruturado com `correlation_id`.
- [ ] README explica setup do zero em <10 minutos.
- [ ] Nenhum segredo ou string de conexão está versionado.
- [ ] Repositório organizado, commits atômicos, mensagens claras.

---

**Fim da Especificação Técnica.**
