# Especificação do Modo Explicação — Claro Flow Engine (CFE)

**Projeto:** Claro Flow Engine (CFE) — Protótipo Funcional
**Time:** Horizon (FIAP 4SI / Challenge Claro 2026)
**Versão:** 1.0
**Público-alvo:** desenvolvedor(a) e/ou agente de IA responsável pela codificação
**Complementa:** spec-funcional.md e spec-tecnica.md

---

## 1. Visão geral e motivação

O CFE em operação normal executa em menos de um segundo por requisição. Isso é ótimo para o usuário final, mas é **péssimo para apresentação didática** — a orquestração acontece "invisível". A banca acadêmica vê a mensagem sair do WhatsApp e chegar no App com dados preservados, mas não enxerga o CFE trabalhando.

O **Modo Explicação** resolve esse problema criando um "raio-x" ao vivo do CFE. Quando ativado, o sistema pausa em cada ponto crítico da execução e espera um comando manual para prosseguir, enquanto um painel dedicado mostra em tempo real:

- Qual componente está sendo executado.
- Que dados chegaram e que dados vão sair.
- O que mudou no banco.
- Onde estamos no diagrama de arquitetura.

O objetivo é transformar o CFE de "caixa preta que funciona" em "camada de orquestração visível e defensável" durante a demonstração final.

---

## 2. Conceito e experiência

### 2.1. Fluxo típico durante a demo

1. Apresentador abre o **Painel de Orquestração** (nova tela).
2. Clica em "Iniciar sessão de explicação". O sistema retorna um `explain_session_id`.
3. Apresentador ativa "Modo Explicação" nos canais (chat, App, painel do atendente). Cada canal passa a enviar `X-Explain-Session-Id: {id}` em todas as requisições.
4. Apresentador começa a interagir normalmente com o chat.
5. Ao clicar "Enviar", a requisição chega ao CFE e **para** no primeiro ponto de pausa. O chat mostra "Aguardando explicação..." e a bolinha de digitando.
6. No painel de orquestração, o passo aparece com todos os dados: endpoint, payload, headers, componente atual.
7. Apresentador explica. Clica "Continuar" no painel.
8. O CFE segue para o próximo ponto de pausa. Novo estado aparece no painel.
9. Isso se repete até a resposta ser enviada de volta.
10. Chat recebe a resposta e a UI segue normalmente para a próxima mensagem.
11. Ao final da demo, apresentador clica "Encerrar sessão" no painel. A sessão fica salva no banco para consulta posterior.

### 2.2. Fora do modo explicação

Todas as requisições sem o header `X-Explain-Session-Id` executam normalmente, sem pausar. O modo explicação é **opt-in por requisição**, não uma flag global do servidor. Isso significa que:

- Demos normais (sem pausas) continuam funcionando.
- Testes automatizados não são afetados.
- Múltiplas sessões de explicação poderiam coexistir (embora improvável na prática).

### 2.3. Persistência histórica

Cada sessão de explicação fica salva no banco com todos os passos. Isso permite:

- Revisar depois o que aconteceu.
- Gerar um replay estático (visualização sem pausas) para incluir no material da apresentação.
- Auditar o comportamento do CFE em qualquer sessão passada.

---

## 3. Pontos de pausa (breakpoints)

Todos os pontos naturais onde faz sentido pausar para explicar. O código dos services chama `_explainer.PauseAsync(stepType, snapshot)` nesses pontos.

### 3.1. Categorias e steps

**Categoria: Entrada da requisição**

| Step | Onde é disparado | O que mostra |
|---|---|---|
| `request_received` | Middleware, antes do controller | Método HTTP, path, headers, body do request |
| `channel_authenticated` | Após validação do `X-Channel-Token` | Canal identificado, token usado |

**Categoria: Identity Module**

| Step | Onde é disparado | O que mostra |
|---|---|---|
| `identity_lookup_started` | Início do `ResolveAsync` | Par `{channel, identifier}` recebido |
| `identity_link_found` | Se link existente | Link encontrado, `unified_customer_id` retornado |
| `identity_customer_lookup_by_cpf` | Se link não existe mas há CPF | Busca em `customers` |
| `identity_customer_created` | Se cliente novo é criado | Novo registro em `customers` |
| `identity_link_created` | Link criado para canal atual | Novo registro em `identity_links` |
| `identity_resolved` | Resposta final montada | Snapshot completo da resolução |

**Categoria: Context Module**

| Step | Onde é disparado | O que mostra |
|---|---|---|
| `context_open_started` | Início do `OpenAsync` | Payload de abertura |
| `context_existing_journey_check` | Verificação de idempotência | Se existe jornada `open` para o cliente + intent |
| `context_journey_created` | Nova jornada criada | INSERT em `journey_contexts` |
| `context_update_started` | Início do `UpdateAsync` | ID + payload_merge |
| `context_journey_updated` | Após UPDATE | Estado antes/depois do payload |
| `context_expiration_check` | Regra reativa | Comparação `updated_at` vs 24h |
| `context_journey_expired` | Se expirou | Mudança de status para `expired` |
| `context_close_started` | Início do `CloseAsync` | `outcome` recebido |
| `context_journey_closed` | Após fechamento | Estado final da jornada |

**Categoria: Handoff Module**

| Step | Onde é disparado | O que mostra |
|---|---|---|
| `handoff_generate_started` | Início do `GenerateAsync` | Journey ID + target channel |
| `handoff_token_created` | Token gerado e salvo | Token, expires_at, target_channel |
| `handoff_deep_link_built` | URL montada | Deep link completo |
| `handoff_resolve_started` | Início do `ResolveTokenAsync` | Token recebido |
| `handoff_token_validated` | Verificações concluídas | Existe? Expirou? Foi usado? |
| `handoff_token_marked_used` | Após marcar como usado | `used_at` preenchido |
| `handoff_identity_link_added` | Novo link para canal de destino | Link do App vinculado ao unified_customer_id |
| `handoff_journey_resumed` | Contexto retornado ao canal | Snapshot do contexto |

**Categoria: Transições e histórico**

| Step | Onde é disparado | O que mostra |
|---|---|---|
| `transition_recorded` | Cada vez que uma transição é gravada | INSERT em `journey_transitions` |

**Categoria: Painel do atendente**

| Step | Onde é disparado | O que mostra |
|---|---|---|
| `panel_query_started` | Consulta do painel | CPF/telefone buscado |
| `panel_context_retrieved` | Contexto retornado | Dados do cliente + jornada + histórico |
| `panel_access_logged` | Auditoria registrada | Transição `panel_accessed` |

**Categoria: Resposta**

| Step | Onde é disparado | O que mostra |
|---|---|---|
| `response_ready` | Antes de enviar resposta HTTP | Status code, body, tempo total |

### 3.2. Nível de detalhe controlado pela sessão

Para não poluir demos rápidas, a sessão de explicação tem um parâmetro `granularity`:

- `low`: apenas 4-5 pontos principais (`identity_resolved`, `context_journey_created`, `handoff_token_created`, `handoff_journey_resumed`, `response_ready`).
- `medium`: 8-10 pontos, um por operação relevante.
- `high`: todos os pontos listados acima (~25 pontos, granularidade máxima).

O painel decide se pausa ou não com base na configuração da sessão. Steps abaixo da granularidade são registrados mas não pausam.

---

## 4. Arquitetura técnica do Explainer

### 4.1. Componentes

```
┌─────────────────────────────────────────────────┐
│              Services (Identity, Context, etc.)   │
│      ↓ chamam _explainer.PauseAsync(...)         │
└─────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────┐
│                ExplainService                    │
│  - PauseAsync(sessionId, stepType, snapshot)     │
│  - ContinueStepAsync(stepId)                     │
│  - StartSessionAsync(granularity)                │
│  - EndSessionAsync(sessionId)                    │
│  - GetWaitingStepsAsync(sessionId)               │
│                                                  │
│  Estado em memória:                              │
│  ConcurrentDictionary<Guid, TaskCompletionSource>│
└─────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────┐
│            SnapshotBroadcaster (SSE)             │
│  Notifica clientes conectados quando um step     │
│  entra em estado "waiting"                       │
└─────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────┐
│      Painel de Orquestração (browser, SSE)       │
│  - Recebe eventos em tempo real                  │
│  - Renderiza estado atual                        │
│  - POST /explain/steps/{id}/continue             │
└─────────────────────────────────────────────────┘
```

### 4.2. Mecanismo de pausa (C#)

Cada chamada a `PauseAsync` cria um `TaskCompletionSource<bool>` mantido em um dicionário concorrente indexado pelo ID do step. O método aguarda esse TCS ser completado (via `ContinueStepAsync`) ou expirar por timeout (5 minutos de segurança).

```csharp
public async Task PauseAsync(
    string stepType,
    object snapshot,
    CancellationToken cancellationToken)
{
    // Se não há sessão ativa no request, ignora silenciosamente
    var sessionId = GetSessionIdFromHttpContext();
    if (sessionId is null) return;

    var session = await LoadSessionAsync(sessionId.Value, cancellationToken);
    if (session is null || session.Status != ExplainSessionStatus.Active)
        return;

    // Verifica granularidade: se este step está abaixo do nível configurado, apenas registra sem pausar
    var shouldPause = ShouldPauseForStep(stepType, session.Granularity);

    var step = new ExplainStep
    {
        Id = Guid.NewGuid(),
        SessionId = session.Id,
        StepType = stepType,
        Snapshot = JsonSerializer.Serialize(snapshot),
        Sequence = await NextSequenceAsync(session.Id, cancellationToken),
        Status = shouldPause ? "waiting" : "auto_continued",
        WaitingSince = DateTime.UtcNow,
        ResolvedAt = shouldPause ? null : DateTime.UtcNow
    };

    _db.ExplainSteps.Add(step);
    await _db.SaveChangesAsync(cancellationToken);

    if (!shouldPause) return;  // registrou mas não pausa

    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    _waiters[step.Id] = tcs;

    await _broadcaster.PublishStepWaitingAsync(step);

    // Timeout de segurança de 5 minutos
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken, timeoutCts.Token);

    try
    {
        await tcs.Task.WaitAsync(linkedCts.Token);
        step.Status = "continued";
    }
    catch (OperationCanceledException)
    {
        step.Status = timeoutCts.IsCancellationRequested ? "timeout" : "cancelled";
        throw;
    }
    finally
    {
        _waiters.TryRemove(step.Id, out _);
        step.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(CancellationToken.None);
    }
}

public Task ContinueStepAsync(Guid stepId)
{
    if (_waiters.TryGetValue(stepId, out var tcs))
    {
        tcs.TrySetResult(true);
    }
    return Task.CompletedTask;
}
```

### 4.3. Isolamento por sessão

O `X-Explain-Session-Id` vem no header do request e é lido via `IHttpContextAccessor` dentro do `ExplainService`. Requests sem header **não pausam nada** — o método `PauseAsync` retorna imediatamente sem custo.

Isso garante que:
- Testes automatizados e chamadas de produção não são afetados.
- Múltiplas sessões podem coexistir (cada requisição tem sua sessão pelo header).
- Ativar/desativar modo explicação é uma configuração de cliente, não de servidor.

### 4.4. Timeouts

Três timeouts em cascata protegem o sistema:

1. **Timeout do step no `PauseAsync`:** 5 minutos. Se apresentador esquecer, o step marca como `timeout` e a execução prossegue (a requisição não trava eternamente).
2. **Timeout do HTTP client no canal:** configurar `HttpClient.Timeout` para 6 minutos (folga sobre o do step).
3. **Cancelamento por desconexão do cliente:** o `CancellationToken` do request cancela o `PauseAsync` se o cliente fechar o browser.

### 4.5. Snapshot: o que gravar

Cada snapshot é um objeto JSON serializado. Deve conter **contexto suficiente para explicar o passo sem consultar outros sistemas**.

Exemplo de snapshot em `identity_resolved`:

```json
{
  "step_type": "identity_resolved",
  "component": "IdentityService",
  "input": {
    "channel": "whatsapp",
    "identifier": "5511999998888",
    "cpf_hint": "12345678900"
  },
  "actions_taken": [
    { "type": "customer_lookup_by_cpf", "found": true },
    { "type": "identity_link_created", "channel": "whatsapp", "identifier": "5511999998888" }
  ],
  "output": {
    "unified_customer_id": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
    "customer": { "full_name": "Ana Silva", "cpf": "12345678900" },
    "was_created": false
  },
  "database_changes": [
    {
      "table": "identity_links",
      "operation": "INSERT",
      "values": { "channel": "whatsapp", "identifier": "5511999998888", "customer_id": "3f2504e0-..." }
    }
  ],
  "elapsed_ms": 12
}
```

Um snapshot bem construído responde às três perguntas didáticas: **O que chegou? O que aconteceu? O que sai?**

---

## 5. Modelo de dados

Adicione ao schema existente do CFE (spec-tecnica.md, §4.2) as tabelas:

```sql
CREATE TABLE explain_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    granularity VARCHAR(10) NOT NULL,       -- 'low', 'medium', 'high'
    status VARCHAR(20) NOT NULL,            -- 'active', 'ended', 'timeout'
    label VARCHAR(200),                     -- rótulo opcional para identificar a sessão
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ended_at TIMESTAMPTZ,
    CONSTRAINT ck_explain_session_granularity
        CHECK (granularity IN ('low', 'medium', 'high')),
    CONSTRAINT ck_explain_session_status
        CHECK (status IN ('active', 'ended', 'timeout'))
);

CREATE INDEX ix_explain_sessions_active
    ON explain_sessions(started_at DESC) WHERE status = 'active';

CREATE TABLE explain_steps (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES explain_sessions(id) ON DELETE CASCADE,
    sequence INT NOT NULL,
    step_type VARCHAR(60) NOT NULL,
    component VARCHAR(60) NOT NULL,         -- 'IdentityService', 'ContextService', etc.
    snapshot JSONB NOT NULL,
    status VARCHAR(20) NOT NULL,            -- 'waiting', 'continued', 'auto_continued', 'timeout', 'cancelled'
    waiting_since TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    resolved_at TIMESTAMPTZ,
    CONSTRAINT ux_explain_steps_session_sequence UNIQUE (session_id, sequence)
);

CREATE INDEX ix_explain_steps_waiting
    ON explain_steps(session_id) WHERE status = 'waiting';
```

Consultas típicas:

- Lista de sessões recentes: `SELECT * FROM explain_sessions ORDER BY started_at DESC LIMIT 20;`
- Steps de uma sessão: `SELECT * FROM explain_steps WHERE session_id = ? ORDER BY sequence;`
- Step atualmente aguardando: `SELECT * FROM explain_steps WHERE session_id = ? AND status = 'waiting' LIMIT 1;`

---

## 6. Contratos de API

### 6.1. Sessões

#### `POST /explain/sessions`

Inicia uma nova sessão de explicação.

**Request:**
```json
{ "granularity": "high", "label": "Demo banca — cenário Ana" }
```

**Response 201:**
```json
{
  "session_id": "8a1c9e2f-4a2b-4d5e-9f3c-1b2d3e4f5a6b",
  "granularity": "high",
  "status": "active",
  "started_at": "2026-07-29T14:00:00Z"
}
```

#### `POST /explain/sessions/{id}/end`

Encerra a sessão. Steps que ainda estejam aguardando são marcados como `cancelled`.

**Response 200:**
```json
{ "session_id": "...", "status": "ended", "ended_at": "2026-07-29T14:35:12Z" }
```

#### `GET /explain/sessions/{id}`

Detalhes da sessão.

**Response 200:**
```json
{
  "id": "...",
  "granularity": "high",
  "status": "active",
  "started_at": "...",
  "ended_at": null,
  "step_count": 12,
  "waiting_step_id": "..."
}
```

#### `GET /explain/sessions/{id}/steps`

Lista todos os steps da sessão (para replay ou tabela histórica no painel).

**Response 200:**
```json
{
  "session_id": "...",
  "steps": [
    {
      "id": "...",
      "sequence": 1,
      "step_type": "request_received",
      "component": "Middleware",
      "snapshot": { "..." },
      "status": "continued",
      "waiting_since": "...",
      "resolved_at": "...",
      "elapsed_ms_waiting": 8123
    },
    ...
  ]
}
```

### 6.2. Steps

#### `POST /explain/steps/{id}/continue`

Libera um step que está aguardando.

**Response 200:**
```json
{ "step_id": "...", "status": "continued", "resolved_at": "..." }
```

**Response 409:** step não está em `waiting`.

### 6.3. Stream em tempo real (SSE)

#### `GET /explain/sessions/{id}/stream`

Endpoint de Server-Sent Events. Mantém conexão aberta e envia eventos conforme steps aparecem/são resolvidos.

Formato SSE:

```
event: step_waiting
data: {"step_id":"...","step_type":"identity_resolved","component":"IdentityService","snapshot":{...}}

event: step_continued
data: {"step_id":"...","resolved_at":"..."}

event: session_ended
data: {"session_id":"...","ended_at":"..."}
```

O painel de orquestração consome esse endpoint via `EventSource` no browser.

### 6.4. Fallback: polling (alternativa ao SSE)

Se SSE causar dor, implemente polling puro:

#### `GET /explain/sessions/{id}/waiting`

Retorna o step atualmente aguardando (ou `null` se nenhum). Painel faz polling a cada 500ms.

---

## 7. Painel de Orquestração (frontend)

### 7.1. Localização

Servido em porta separada dos canais, ex: `http://localhost:5177`. Estrutura em `channels/orchestration-panel/`:

```
channels/orchestration-panel/
├── index.html
├── styles.css
├── app.js                # lógica principal + conexão SSE
├── diagram.js            # renderização do diagrama animado
└── config.js             # URL da API
```

### 7.2. Layout

Tela dividida em 3 colunas mais um cabeçalho:

**Cabeçalho:**
- Título "Painel de Orquestração — Claro Flow Engine"
- Status da sessão: sem sessão / ativa / encerrada
- Botões: "Nova sessão", "Encerrar sessão", "Modo replay"
- Contador de steps: `12 executados · 1 aguardando · 0 pendentes`
- Seletor de granularidade (habilitado só ao criar sessão)

**Coluna esquerda — Timeline (30% da largura):**

Lista vertical de todos os steps já executados na sessão, com scroll. Cada item:

```
┌────────────────────────────────────┐
│ 12  ● IDENTITY_RESOLVED            │
│    IdentityService                 │
│    há 3s                            │
├────────────────────────────────────┤
│ 11  ● CONTEXT_JOURNEY_CREATED      │
│    ContextService                  │
│    há 8s                            │
├────────────────────────────────────┤
│ 10  ✓ REQUEST_RECEIVED             │
│    Middleware                      │
│    há 12s                           │
└────────────────────────────────────┘
```

Símbolos:
- `●` — step atual (destacado, animado)
- `✓` — step concluído
- `⏱` — step em timeout
- `⊘` — step cancelado

Clicar em um step passado abre modal com o snapshot dele.

**Coluna central — Diagrama arquitetural (45% da largura):**

Reprodução do diagrama de componentes do Sprint 2 (Figura 1) em SVG interativo, com destaque no componente ativo:

```
┌─────────────────────────────────────────────┐
│   CANAIS                                    │
│   [WhatsApp] [App] [Painel]                 │
├─────────────────────────────────────────────┤
│                    │                        │
│                    ▼                        │
│   ┌─────────────────────────────────┐       │
│   │      API Gateway / Middleware   │       │
│   └─────────────────────────────────┘       │
│                    │                        │
│                    ▼                        │
│   ┌───────────┐ ┌──────────┐ ┌──────────┐  │
│   │ Identity  │ │ Context  │ │ Handoff  │  │
│   │ ★ ATIVO   │ │          │ │          │  │
│   └───────────┘ └──────────┘ └──────────┘  │
│                    │                        │
│                    ▼                        │
│   ┌─────────────────────────────────┐       │
│   │        PostgreSQL               │       │
│   └─────────────────────────────────┘       │
└─────────────────────────────────────────────┘
```

O componente ativo pisca em cor de destaque. Setas mostram fluxo da requisição.

**Coluna direita — Detalhes do step atual (25%):**

Painel dinâmico com o snapshot do step aguardando:

```
┌────────────────────────────────────┐
│ STEP: identity_resolved            │
│ Componente: IdentityService        │
│ Sequência: #12                     │
│ Aguardando há: 4s                   │
│                                    │
│ ── ENTRADA ───────────────────────  │
│ { channel: "whatsapp",             │
│   identifier: "551199...",         │
│   cpf_hint: "12345678900" }        │
│                                    │
│ ── AÇÕES REALIZADAS ──────────────  │
│ • Busca customer por CPF: OK       │
│ • Criação de identity_link         │
│                                    │
│ ── ALTERAÇÕES NO BANCO ───────────  │
│ INSERT identity_links              │
│   channel: whatsapp                │
│   identifier: 551199...            │
│                                    │
│ ── SAÍDA ─────────────────────────  │
│ { unified_customer_id: "...",      │
│   customer: {full_name:"Ana",...}, │
│   was_created: false }             │
│                                    │
│ ┌─────────────────────────────┐    │
│ │   ▶ CONTINUAR               │    │
│ └─────────────────────────────┘    │
└────────────────────────────────────┘
```

Botão "Continuar" chama `POST /explain/steps/{id}/continue`.

### 7.3. Conexão em tempo real

Ao criar/entrar em uma sessão, o painel abre `EventSource('/explain/sessions/{id}/stream')`.

```js
const es = new EventSource(`${API}/explain/sessions/${sessionId}/stream`);

es.addEventListener('step_waiting', e => {
  const step = JSON.parse(e.data);
  timeline.addStep(step);
  detailPanel.showStep(step);
  diagram.highlightComponent(step.component);
});

es.addEventListener('step_continued', e => {
  const { step_id } = JSON.parse(e.data);
  timeline.markContinued(step_id);
  detailPanel.clear();
  diagram.clearHighlight();
});

es.addEventListener('session_ended', e => {
  showSessionEnded();
  es.close();
});
```

### 7.4. Modo replay

Permite abrir uma sessão histórica e "avançar" pelos steps sem interagir com o CFE real. Útil para revisar demos passadas ou construir slides.

Implementação simples: `GET /explain/sessions/{id}/steps` retorna todos, e o painel renderiza um por vez com botão "Próximo" local (sem chamar `continue`).

---

## 8. Integração nos canais existentes

Cada canal (chat, App, painel do atendente) precisa saber se está participando de uma sessão de explicação.

### 8.1. Ativação por canal

Cada canal ganha um pequeno controle no cabeçalho:

```
┌─────────────────────────────────────────┐
│ Claro Atendimento         [🎓 Explicar]  │
└─────────────────────────────────────────┘
```

Clicar em "🎓 Explicar" abre modal simples pedindo o `explain_session_id` (que o apresentador copiou do painel de orquestração). Após colar, o canal salva o ID em `localStorage` e passa a enviar o header `X-Explain-Session-Id` em todas as requisições.

Quando ativo, o canal exibe uma faixa persistente no topo:

```
┌─────────────────────────────────────────┐
│ 🎓 MODO EXPLICAÇÃO ATIVO — sessão a1b2  │
│    Timeout HTTP: 6min                    │
└─────────────────────────────────────────┘
```

E um botão "Desativar" que remove o header.

### 8.2. Ajustes de client HTTP

Aumentar o timeout do fetch para 6 minutos (folga sobre o timeout de 5 min do backend):

```js
async function apiCall(path, options = {}) {
  const explainId = localStorage.getItem('explain_session_id');
  const headers = { ...options.headers, 'X-Channel-Token': CHANNEL_TOKEN };
  if (explainId) headers['X-Explain-Session-Id'] = explainId;

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 6 * 60 * 1000);

  try {
    return await fetch(`${API}${path}`, {
      ...options,
      headers,
      signal: controller.signal
    });
  } finally {
    clearTimeout(timeout);
  }
}
```

### 8.3. Feedback visual durante pausa

Enquanto a requisição está pausada no CFE, o canal deve mostrar feedback claro:

- **Chat:** ao invés do "digitando..." curto, mostra "🎓 Aguardando explicação no painel..." com animação de reticências.
- **App:** overlay com spinner e texto "Aguardando explicação — clique em Continuar no painel de orquestração".
- **Painel do atendente:** faixa sobre a área de conteúdo indicando que a atualização está pausada.

Isso previne a impressão de que a demo travou.

---

## 9. Ciclo de vida da sessão

### 9.1. Estados

```
      criada
        │
        ▼
    ┌───────┐
    │active │◄── steps podem ser criados/pausados/continuados
    └───┬───┘
        │
        │ (POST /end)                (timeout automático — não implementado no MVP)
        ▼                             (deixado para evolução futura)
    ┌───────┐
    │ ended │
    └───────┘
```

Uma sessão sem `end` explícito permanece `active` no banco indefinidamente. Isso é aceitável para o MVP — no futuro, um job pode encerrar sessões inativas há mais de 1 hora.

### 9.2. Comportamento no encerramento

Ao chamar `POST /explain/sessions/{id}/end`:

1. Sessão marca `ended_at` e `status='ended'`.
2. Todos os steps `waiting` são marcados como `cancelled`.
3. Os TCS pendentes são cancelados (`SetException(new OperationCanceledException())`), o que faz o `PauseAsync` lançar e o request no canal receber 500.
4. O SSE stream emite `session_ended` e fecha a conexão.

### 9.3. O que fazer se o painel cair

Se o browser do apresentador cair no meio de uma sessão:

- Steps já em `waiting` continuam esperando os 5min de timeout.
- Reabrir o painel na mesma sessão (`GET /explain/sessions/{id}`) reconecta e mostra o estado atual.
- Se algum step deu timeout enquanto o painel estava fora, a requisição no canal já falhou (isso é aceitável).

---

## 10. Considerações técnicas

### 10.1. Concorrência

O dicionário de `TaskCompletionSource` é `ConcurrentDictionary` — seguro para múltiplas threads. Cada request roda em sua própria Task, então múltiplos steps podem estar aguardando simultaneamente (por exemplo, se você tiver duas abas de chat abertas ambas em modo explicação).

### 10.2. Threading e RunContinuationsAsynchronously

O TCS deve ser criado com `TaskCreationOptions.RunContinuationsAsynchronously`. Sem essa flag, chamar `SetResult` pode executar a continuação sincronamente na thread que chamou `Continue`, causando comportamento inesperado (e potencialmente segurando o request de `/continue` até a próxima pausa).

### 10.3. Idempotência do `continue`

Chamar `continue` duas vezes no mesmo step retorna 409 na segunda chamada. Isso previne cliques duplos e comportamento imprevisível.

### 10.4. Escopo da injeção do ExplainService

`ExplainService` deve ser **Singleton** — o dicionário de TCS precisa ser compartilhado entre todas as requisições. As dependências mutáveis dele (como `DbContext`) devem ser resolvidas via `IServiceScopeFactory` dentro dos métodos, não injetadas diretamente.

```csharp
public class ExplainService : IExplainService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public async Task PauseAsync(...)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        // ...
    }
}
```

### 10.5. Serialização de snapshots

Snapshots grandes podem impactar performance da UI. Regras:

- Truncar valores muito longos (ex: strings >1000 chars, arrays >50 itens).
- Remover campos irrelevantes antes de serializar (não incluir hashes internos, timestamps redundantes).
- Se o payload da jornada crescer, snapshot mostra só chaves relevantes.

### 10.6. Segurança

O modo explicação **expõe dados internos do CFE** (payloads, IDs internos, estados de banco). Em produção, o modo explicação deveria:

- Exigir autenticação de admin.
- Ser desabilitado ou muito restrito.
- Log de auditoria específico.

Para o protótipo acadêmico, nada disso é necessário — deixamos comentário no código indicando essa dívida técnica.

### 10.7. Performance sem sessão

`PauseAsync` sem sessão ativa custa apenas uma leitura de `HttpContext.Request.Headers`. É `O(1)` e não impacta requests normais. Se a leitura for chata, um `if (!SessionActive) return;` bem no topo do método garante custo mínimo.

---

## 11. Priorização e ordem de desenvolvimento

O modo explicação entra **após** o MVP funcional estar rodando. Ele é COULD-becomes-MUST no cronograma estendido (7-8 dias).

Ordem sugerida dentro dos dias dedicados:

### Dia 5 — Backend do Explainer

1. Criar tabelas `explain_sessions` e `explain_steps` via nova migration.
2. Implementar `IExplainService` + `ExplainService` (Singleton) com `TaskCompletionSource`.
3. Implementar `SnapshotBroadcaster` (SSE via `IAsyncEnumerable` ou `Channel<T>`).
4. Criar `ExplainController` com todos os endpoints do §6.
5. Injetar `IExplainService` nos três services existentes (Identity, Context, Handoff).
6. Adicionar chamadas `PauseAsync` em cada ponto do §3.
7. Testar sem front: iniciar sessão via Swagger, disparar chamada em outro terminal via curl com header, observar que pausa.

### Dia 6 — Painel de Orquestração

1. Criar estrutura de arquivos em `channels/orchestration-panel/`.
2. Implementar controles do cabeçalho (nova sessão, encerrar, seletor de granularidade).
3. Renderizar timeline à esquerda com `<ul>` dinâmico.
4. Renderizar diagrama SVG com IDs nos componentes.
5. Implementar coluna direita com detalhes do step atual.
6. Conectar SSE, atualizar UI conforme eventos chegam.
7. Botão "Continuar" que chama a API.

### Dia 7 — Integração nos canais

1. Adicionar controle "🎓 Explicar" no cabeçalho de cada canal.
2. Modal de ativação (colar session_id).
3. Wrapper de `fetch` que adiciona header.
4. Feedback visual durante pausa (spinner, faixa).
5. Testar cada canal em modo explicação.

### Dia 8 — Polimento e ensaio

1. Rodar os 3 cenários (Ana, Carlos, Mariana) em modo explicação com granularidade `high`.
2. Ajustar snapshots que estejam muito verbosos ou pouco claros.
3. Polir CSS do painel de orquestração.
4. Atualizar README com seção sobre modo explicação.
5. Ensaiar demo com pausas.

---

## 12. Critérios de aceite (DoD do modo explicação)

- [ ] Consigo iniciar uma sessão via `POST /explain/sessions` e recebo `session_id`.
- [ ] Painel de orquestração abre, conecta via SSE e mostra "aguardando primeiro step".
- [ ] Ativo modo explicação no chat, envio uma mensagem. Chat mostra "aguardando explicação".
- [ ] Painel mostra o primeiro step com snapshot completo.
- [ ] Clico "Continuar". Chat progride para próximo estado do bot. Novo step aparece no painel.
- [ ] Fluxo completo (chat → deep link → App → confirmação) executa passo a passo com todos os pontos aparecendo no painel.
- [ ] Encerro sessão. Painel mostra sessão encerrada e desabilita controles.
- [ ] Requisições sem `X-Explain-Session-Id` executam normalmente, sem pausar.
- [ ] Se eu esquecer de continuar por 5 minutos, o step timeout e o canal recebe erro (comportamento controlado, não trava eternamente).
- [ ] Se eu recarregar o painel no meio da sessão, ele reconecta e mostra estado atual.

---

**Fim da Especificação do Modo Explicação.**
