# Especificação Funcional — Claro Flow Engine (CFE)

**Projeto:** Claro Flow Engine (CFE) — Protótipo Funcional
**Time:** Horizon (FIAP 4SI / Challenge Claro 2026)
**Versão:** 1.0
**Público-alvo deste documento:** desenvolvedor(a) e/ou agente de IA responsável pela codificação do protótipo

---

## 1. Visão geral

O **Claro Flow Engine (CFE)** é uma camada de orquestração conversacional posicionada logicamente entre os canais de atendimento da Claro e seus sistemas de backend. Sua função é garantir **continuidade de jornada** entre pontos de contato — o cliente inicia uma interação em um canal e retoma em outro sem repetir informações, sem perder contexto e sem recomeçar do zero.

O protótipo aqui descrito **não é uma simulação encenada** de uma conversa específica. É um sistema real com regras de negócio genéricas, validações efetivas e persistência real em banco de dados. Qualquer cliente cadastrado no seed pode iniciar uma jornada; qualquer token válido deve ser resolvido; qualquer atendente pode consultar qualquer contexto ativo. Os cenários descritos no Sprint 2 (Ana, Carlos, Mariana) servem como **casos de validação**, não como scripts implementados.

O CFE se apoia em três capacidades centrais:

1. **Resolução de identidade unificada** — reconhecer o mesmo cliente independentemente do identificador usado em cada canal (telefone, CPF, login, etc.).
2. **Persistência de contexto de jornada** — manter em tempo real qual intenção o cliente declarou, em qual canal iniciou, em qual etapa parou e quais dados já forneceu.
3. **Handoff entre canais** — transferir uma jornada de um canal para outro por meio de um deep link com token de contexto, sem exigir reintrodução de dados.

Complementa essas capacidades um **painel de contexto para atendimento humano**, em modo somente leitura, que exibe em tempo real o histórico da jornada do cliente que está em atendimento.

---

## 2. Objetivos do protótipo

O protótipo tem por objetivo demonstrar, de forma navegável e defensável, que:

- Um cliente pode iniciar uma jornada em um canal simulado (chat estilo WhatsApp), fornecer identidade e intenção, e ter esses dados persistidos no CFE.
- O CFE consegue gerar um deep link que, aberto no App simulado, recupera o contexto real da jornada e evita repetição.
- Um atendente humano consegue visualizar, em tempo real, o histórico completo de qualquer jornada ativa via painel.
- O sistema trata corretamente situações reais: cliente inexistente, CPF inválido, plano indisponível, token expirado, jornada abandonada.
- A arquitetura é modular e permite adição futura de canais e intenções sem reformulação estrutural.

---

## 3. Escopo do protótipo (MVP)

**Dentro do escopo:**

- API única em .NET expondo todos os endpoints do CFE (Identity, Context, Handoff).
- Persistência em PostgreSQL com schema completo e dados mockados via seed.
- Canal simulado tipo WhatsApp (chat web próprio), com máquina de estados para conduzir a conversa.
- Canal simulado App Minha Claro (web), que abre via deep link, resolve o token e apresenta a tela de confirmação com dados preservados.
- Painel do atendente (web), que consulta o CFE em tempo real (polling) e exibe histórico completo da jornada.
- Uma intenção suportada: **troca de plano**.
- Ciclo de vida completo da jornada: aberta, atualizada, encerrada (concluída, abandonada) e expirada por inatividade.
- Rastreabilidade: todas as transições e acessos ficam registrados em tabela auditável.
- Health check da API e logs estruturados.

**Fora do escopo do protótipo:**

- Integração com o WhatsApp oficial ou qualquer API real da Claro.
- Autenticação real do atendente (login).
- Autenticação real por token de serviço entre canais e CFE (usaremos header mockado).
- Notificação de falhas via webhook/e-mail em produção.
- Outros canais (Alexa, RCS, SMS, USSD, Site, etc.).
- Outras intenções além de troca de plano.
- Métricas operacionais no painel (TMA, taxa de abandono etc.).
- Painel em React (usaremos HTML/CSS/JS puro por questão de tempo; funcionalmente equivalente).
- Alertas automáticos por degradação.

---

## 4. Atores

| Ator | Descrição | Interage com |
|---|---|---|
| **Cliente Claro** | Usuário final; inicia jornada no chat, recebe deep link, retoma no App | Chat simulado, App simulado |
| **Bot WhatsApp** | Componente conversacional automatizado (máquina de estados) que conduz a coleta | Chat simulado, API do CFE |
| **App Minha Claro** | Canal de destino do handoff; consome token do deep link | App simulado, API do CFE |
| **Atendente Humano** | Consulta contexto de jornada ativa em tempo real | Painel do atendente, API do CFE |

---

## 5. Casos de uso detalhados

### UC01 — Iniciar jornada

**Ator principal:** Cliente Claro (via Bot WhatsApp)

**Pré-condições:**
- Sistema disponível.
- Cliente tem acesso ao chat simulado.

**Fluxo principal:**
1. Cliente abre o chat simulado.
2. Bot cumprimenta e pergunta em que pode ajudar.
3. Cliente informa a intenção (nesta fase: variantes de "trocar de plano").
4. Bot solicita CPF para confirmar identidade.
5. Cliente informa CPF.
6. Sistema aciona **UC02 (Resolver identidade unificada)**.
7. Sistema abre formalmente a jornada (`POST /context/open`), registrando canal de origem, intenção, etapa atual e horário.
8. Bot confirma abertura e prossegue.

**Fluxos alternativos:**
- **A1.** Cliente informa intenção não suportada → Bot informa que atualmente só troca de plano está disponível e encerra ou aguarda nova intenção.
- **A2.** CPF em formato inválido (menos de 11 dígitos, caracteres inválidos) → Bot pede novo CPF.

**Pós-condições:**
- Jornada existe no banco com status `open`.
- Transição `journey_started` registrada.

---

### UC02 — Resolver identidade unificada

**Ator principal:** Bot WhatsApp (indiretamente, o Cliente)

**Pré-condições:**
- Um identificador foi coletado (CPF, telefone, login do App).

**Fluxo principal:**
1. Sistema recebe um par `{channel, identifier}` (ex: `{whatsapp, "5511999998888"}`, `{cpf, "12345678900"}` ou `{app, "user_abc"}`).
2. Sistema busca em `identity_links` se algum link já existe para esse par.
3. Se existir, retorna o `unified_customer_id` associado.
4. Se não existir mas o identificador é um CPF, busca em `customers` por CPF; se encontrado, cria o link e retorna o `unified_customer_id`.
5. Se não existir e o CPF não está cadastrado, **UC03 (Registrar novo cliente)** é acionado.
6. Todo acesso é logado com `channel`, `identifier` e resultado.

**Fluxos alternativos:**
- **A1.** Identificador em formato inválido → retorna erro 400 com mensagem clara.

**Pós-condições:**
- Existe um `unified_customer_id` associado ao par informado.
- Link registrado em `identity_links`.

---

### UC03 — Registrar novo cliente

**Ator principal:** Bot WhatsApp

**Pré-condições:**
- Cliente forneceu CPF válido e o CPF não está cadastrado.

**Fluxo principal:**
1. Bot informa que o cliente parece ser novo e solicita nome completo.
2. Cliente fornece nome.
3. Sistema cria registro em `customers` (CPF + nome) e cria link em `identity_links` para o canal atual.
4. Sistema retorna `unified_customer_id`.

**Fluxo alternativo:**
- **A1.** Cliente se recusa a fornecer nome → Bot encerra a jornada informando que a identificação é necessária.

**Pós-condições:**
- Novo registro em `customers`.
- Link registrado em `identity_links` para o canal de origem.

---

### UC04 — Atualizar contexto de jornada

**Ator principal:** Qualquer canal com jornada aberta

**Pré-condições:**
- Existe uma jornada com status `open` associada ao cliente.

**Fluxo principal:**
1. Canal envia `PATCH /context/{id}` com as informações novas: nova etapa, novos dados coletados, ou ambos.
2. Sistema valida que a jornada existe e está aberta.
3. Sistema atualiza `current_step`, `payload` e `updated_at`.
4. Sistema registra uma `journey_transition` com `event_type = step_updated`.

**Fluxos alternativos:**
- **A1.** Jornada não existe → 404.
- **A2.** Jornada existe mas não está aberta (concluída, expirada, abandonada) → 409 com estado atual.

**Pós-condições:**
- Contexto atualizado em `journey_contexts`.
- Transição registrada.

---

### UC05 — Gerar deep link para handoff

**Ator principal:** Bot WhatsApp

**Pré-condições:**
- Jornada aberta com dados suficientes para retomada (intenção declarada e ao menos identidade resolvida).

**Fluxo principal:**
1. Bot chama `POST /handoff/generate` informando o `journey_context_id` e o canal de destino (ex: `app`).
2. Sistema gera um token único (UUID ou random-string), com validade de **30 minutos**.
3. Sistema registra o token em `handoff_tokens`, associado à jornada e ao canal de destino.
4. Sistema retorna URL completa do deep link, ex: `http://localhost:5173/journey?token=abc123`.
5. Sistema registra transição `deep_link_generated`.

**Fluxos alternativos:**
- **A1.** Jornada não existe ou não está aberta → 409.

**Pós-condições:**
- Token válido persistido.
- Deep link disponível para envio ao cliente.

---

### UC06 — Retomar jornada em outro canal

**Ator principal:** App Minha Claro

**Pré-condições:**
- Cliente tocou em um deep link válido.

**Fluxo principal:**
1. App abre e captura o `token` da URL.
2. App exibe tela de login (login mockado — qualquer credencial válida serve, opcionalmente ligada a um CPF cadastrado).
3. Após login, App chama `GET /context/resolve?token={token}`.
4. Sistema valida o token: existe? Não foi usado? Ainda está dentro do prazo (`expires_at > now`)?
5. Se válido:
   - Retorna o contexto completo da jornada (customer, intent, current_step, payload).
   - Marca token como usado (`used_at = now`).
   - Vincula o identificador do canal atual (App) ao `unified_customer_id` (se ainda não vinculado).
   - Registra transição `journey_resumed`.
6. App renderiza a tela correspondente à etapa da jornada (no caso de troca de plano: tela de confirmação com dados preenchidos).

**Fluxos alternativos:**
- **A1.** Token não existe → 404 "sessão inválida".
- **A2.** Token já foi usado → 410 "sessão já retomada".
- **A3.** Token expirado → 410 "sessão expirada" (mesmo tratamento visual do lado do App: mensagem orientando iniciar nova conversa).

**Pós-condições:**
- Token marcado como usado.
- Link de identidade adicional criado (canal App).
- Transição registrada.

---

### UC07 — Encerrar jornada

**Ator principal:** Qualquer canal onde o cliente finalize a interação (App ou WhatsApp) ou atendente

**Pré-condições:**
- Jornada aberta.

**Fluxo principal:**
1. Canal envia `POST /context/{id}/close` com um `outcome` (`concluded`, `abandoned`).
2. Sistema valida que a jornada existe e está aberta.
3. Sistema atualiza `status`, define `closed_at`.
4. Sistema registra transição `journey_closed`.

**Pós-condições:**
- Jornada com status final.
- Transição registrada.

---

### UC08 — Expirar jornada por inatividade

**Ator principal:** Sistema (regra automática)

**Pré-condições:**
- Jornada com status `open`.
- `updated_at` mais antigo que 24 horas.

**Fluxo principal:**

Para o protótipo, esta regra é implementada **de forma reativa**, não via job de background. Sempre que:

- Um endpoint acessa uma jornada `open`, o sistema verifica se `now - updated_at > 24h`. Se sim, marca como `expired` antes de responder.
- Um token é validado (`/context/resolve`), a mesma verificação ocorre.
- O painel lista jornadas, também aplica a verificação.

Isso simula corretamente o comportamento de expiração sem exigir job agendado, e economiza complexidade no MVP.

**Pós-condições:**
- Jornada com status `expired`.
- Token(s) associado(s) marcados como inválidos por consequência.
- Transição `journey_expired` registrada.

---

### UC09 — Consultar histórico de jornada (painel)

**Ator principal:** Atendente Humano

**Pré-condições:**
- Atendente identifica o cliente (por CPF ou telefone) no painel.

**Fluxo principal:**
1. Atendente digita o identificador (CPF ou telefone) no painel.
2. Painel chama `GET /identity/resolve?channel=cpf&identifier={cpf}` para resolver o `unified_customer_id`.
3. Painel chama `GET /context/customer/{unified_customer_id}` para obter a jornada ativa (ou a mais recente).
4. Painel chama `GET /context/{id}/transitions` para obter o histórico completo.
5. Sistema registra transição `panel_accessed` para auditoria.
6. Painel entra em modo **polling** (a cada 3-5 segundos) e refaz `GET /context/{id}` + `GET /context/{id}/transitions` para atualizar a UI enquanto o atendimento está ativo.

**Fluxos alternativos:**
- **A1.** Cliente não encontrado → painel exibe "cliente não localizado".
- **A2.** Cliente localizado sem jornada ativa → painel exibe "sem jornadas em andamento" + histórico das últimas jornadas (opcional).

**Pós-condições:**
- Acesso auditado em `journey_transitions`.

---

## 6. Regras de negócio

### 6.1. Validação de CPF

- Deve ter exatamente 11 dígitos numéricos (após remoção de pontos e traços).
- **Não é necessário validar dígitos verificadores** no protótipo — os CPFs do seed são fictícios. Basta validar formato.

### 6.2. Formato de identificadores por canal

| Canal | Identificador esperado |
|---|---|
| `whatsapp` | Número de telefone com DDI+DDD, 12-13 dígitos (ex: `5511999998888`) |
| `app` | Login do App (string alfanumérica) |
| `cpf` | CPF sem pontuação (11 dígitos) |

### 6.3. Ciclo de vida da jornada

Estados válidos: `open`, `concluded`, `expired`, `abandoned`.

Transições permitidas:
- `open → concluded` (encerramento explícito com sucesso)
- `open → abandoned` (encerramento explícito com desistência)
- `open → expired` (regra automática de inatividade)

Uma vez fora do estado `open`, a jornada é imutável (nenhum PATCH ou close adicional é permitido).

### 6.4. TTL do deep link (token de handoff)

- **30 minutos** a partir da geração.
- Token é single-use: após ser resolvido com sucesso, `used_at` é preenchido e ele não pode mais ser usado.

### 6.5. TTL da jornada ativa

- **24 horas** sem atualização (`updated_at`).
- Verificação reativa nos endpoints que tocam a jornada (ver UC08).

### 6.6. Uma jornada ativa por cliente por vez

- No MVP, um cliente só pode ter **uma jornada com status `open`** simultânea.
- Se uma nova tentativa de abertura ocorrer com jornada ativa existente, o sistema retorna a jornada existente (idempotência) ou encerra a anterior como `abandoned` antes de abrir uma nova — **decidir na implementação** a política; recomendamos retornar a existente e registrar transição `journey_reopen_attempted`.

### 6.7. Rastreabilidade de acessos

- Toda operação relevante gera uma linha em `journey_transitions`, com `event_type`, `channel`, `description` e `metadata` (JSON).
- Consultas do painel geram transição do tipo `panel_accessed`.

### 6.8. Idempotência recomendada

- Endpoints de escrita devem tolerar retentativas idempotentes onde fizer sentido (ex: `open` retorna a jornada existente; `close` em jornada já fechada retorna 200 com estado atual em vez de 409, ou 409 explícito — decidir e documentar).

---

## 7. Máquinas de estado

### 7.1. Journey Status

```
             ┌─────────┐
             │  open   │◄──── criação
             └────┬────┘
                  │
      ┌───────────┼───────────┐
      ▼           ▼           ▼
 concluded    abandoned    expired
 (final)      (final)      (final)
```

### 7.2. Bot Conversation (chat simulado — intenção "troca de plano")

```
   ┌────────────┐
   │  greeting  │
   └─────┬──────┘
         ▼
   ┌─────────────────┐
   │ awaiting_intent │
   └─────┬───────────┘
         │ (intenção reconhecida = "trocar de plano")
         ▼
   ┌──────────────┐
   │ awaiting_cpf │◄──── loop se CPF inválido
   └─────┬────────┘
         │ (CPF válido)
         ▼
   ┌──────────────────────┐
   │  identity_resolved   │
   │  (opc: coleta nome   │
   │   se cliente novo)   │
   └─────┬────────────────┘
         ▼
   ┌───────────────────────┐
   │  awaiting_plan_choice │◄──── loop se escolha inválida
   └─────┬─────────────────┘
         │ (plano válido)
         ▼
   ┌──────────────────┐
   │  link_generated  │
   └─────┬────────────┘
         ▼
   ┌────────────┐
   │  completed │ (bot informa que finalize no App)
   └────────────┘
```

Estados de erro tratados: `cpf_invalid_retry`, `plan_invalid_retry`, `intent_not_supported`.

O bot precisa **guardar em memória de sessão** (server-side) qual estado a conversa está, e reagir a cada mensagem de acordo com o estado atual. A cada avanço válido, o bot chama o CFE (`/context/open` ou `/context/{id}`) para persistir a etapa. Assim, o estado do bot fica sincronizado com o estado da jornada no CFE.

---

## 8. Comportamento das interfaces

### 8.1. Chat simulado (canal WhatsApp)

- Layout em coluna estilo mensageiro (bolhas de mensagem, campo de input, botão enviar).
- O bot responde a cada mensagem do cliente conforme a máquina de estados (7.2).
- **Badges internos do CFE** aparecem visíveis (com estilo diferenciado) para tornar a orquestração perceptível na demo — por exemplo, após `POST /identity/resolve`, aparece uma pequena tag `CFE — identidade resolvida · unified_customer_id vinculado`. Isso é uma escolha didática, e reforça a visualização do funcionamento (equivalente aos wireframes do Sprint 2).
- Ao final, o bot exibe um "card" clicável com o deep link. O card mostra: título ("Continuar troca de plano"), status ("Seus dados já estão preenchidos") e botão "Continuar no App".

### 8.2. App Minha Claro simulado

- Abre pela URL `?token=xxx`.
- Primeiro exibe tela de login (formulário mockado; qualquer usuário/senha válida serve — não valida senha, só formato).
- Após login, chama `GET /context/resolve?token=xxx`.
- Renderiza a tela de confirmação de troca de plano:
  - Banner: "Contexto de jornada recuperado pelo CFE — continuando sua solicitação iniciada no WhatsApp."
  - Dados preenchidos: nome, CPF, plano atual, novo plano, valor.
  - Botão "Confirmar troca de plano" → chama `POST /context/{id}/close` com `outcome=concluded`.
  - Botão "Cancelar" → chama `POST /context/{id}/close` com `outcome=abandoned`.
- Em caso de token inválido/expirado/usado: exibe tela de "Sessão expirada" com CTA "Abrir chat da Claro novamente".

### 8.3. Painel do atendente

- Layout: sidebar esquerda com identificação do atendente (mockada, texto simples "Júlia Souza — Em atendimento") e navegação; área principal com dados do cliente + histórico.
- Campo no topo para buscar por CPF ou telefone.
- Após localizar, exibe:
  - **Bloco Dados do Cliente:** nome, CPF, telefone, plano atual, segmento.
  - **Bloco Status da Jornada:** em andamento (com badge), canal de origem, canal atual, intenção, última ação (tempo relativo).
  - **Bloco Histórico da Jornada:** timeline ordenada da mais recente para a mais antiga, com ícone, título, descrição, canal e horário de cada transição.
- **Polling:** a cada 3-5 segundos, refaz as chamadas `GET /context/{id}` e `GET /context/{id}/transitions` para manter o painel atualizado em tempo real. Se detectar mudança, atualiza a UI sem recarregar a página.
- Aviso visível: "Este painel exibe o contexto de jornada em modo somente leitura. Nenhuma alteração pode ser feita aqui. Para atualizar dados do cliente, utilize o sistema CRM."

---

## 9. Priorização (MoSCoW)

### MUST (sem isso não há entrega)

- API .NET com os 3 módulos internos (Identity, Context, Handoff) e todos os endpoints listados na spec técnica.
- PostgreSQL com schema completo + seed de dados mockados (clientes, planos).
- Chat simulado com máquina de estados para a intenção "troca de plano".
- App simulado com resolução de deep link e tela de confirmação.
- Painel do atendente com busca por CPF/telefone, exibição do contexto e histórico, e polling.
- Regra de expiração de jornada por inatividade (verificação reativa).
- Regra de TTL de token (30 min).
- Health check endpoint (`/health`).

### SHOULD (fortemente recomendado, ~2-3h cada)

- Logs estruturados com Serilog (correlation por journey_id).
- Swagger habilitado com descrições nos endpoints.
- Tratamento consistente de erros (respostas JSON padronizadas).
- Badges de ação do CFE visíveis no chat simulado.
- Tela de "sessão expirada" no App simulado.

### COULD (só se sobrar tempo)

- Endpoint `GET /context/active` que lista todas as jornadas abertas (dá base pra futura implementação de "lista de jornadas em andamento" no painel).
- Bot com variações de resposta (não sempre a mesma frase).
- CSS mais polido.
- README com print da arquitetura.

### WON'T (fica de fora do protótipo, entra como roadmap)

- Autenticação real por JWT/token de serviço.
- Login do atendente no painel.
- Integração real com WhatsApp Business API ou Telegram.
- Painel em React.
- Alertas via webhook/e-mail em degradação.
- Métricas operacionais no painel.
- Outros canais (Alexa, Site, etc.).
- Outras intenções.
- Job de background para expiração ativa (a regra reativa cobre a demo).

---

## 10. Ordem de desenvolvimento preferencial (5 dias)

A ordem abaixo garante que ao final de cada dia haja um **artefato funcional testável**, mesmo que incompleto. É preferível terminar o Dia 3 com um cenário rodando ponta a ponta do que terminar o Dia 5 com tudo pela metade.

### Dia 1 — Fundação backend

**Meta ao fim do dia:** API rodando localmente + banco criado + endpoint `/identity/resolve` funcionando.

1. Criar solution + projeto .NET Web API.
2. Subir PostgreSQL via Docker Compose.
3. Modelar as entidades (Customer, IdentityLink, Plan, CustomerPlan, JourneyContext, JourneyTransition, HandoffToken).
4. Configurar EF Core + primeira migration.
5. Criar seed de dados mockados (3-4 clientes, 4-5 planos).
6. Implementar `IdentityService` + `IdentityController` com `POST /identity/resolve` e lógica de criação de link.
7. Habilitar Swagger e testar todos os fluxos de resolução via UI do Swagger.

### Dia 2 — Núcleo de contexto e handoff

**Meta ao fim do dia:** Cenário "criação → atualização → link → resolução → fechamento" funcionando **via Postman/Swagger**, sem UI ainda.

1. Implementar `ContextService` + `ContextController` com `POST /context/open`, `PATCH /context/{id}`, `GET /context/{id}`, `GET /context/customer/{customerId}`, `POST /context/{id}/close`.
2. Implementar `HandoffService` + `HandoffController` com `POST /handoff/generate` e `GET /context/resolve?token=`.
3. Implementar registro automático de `journey_transitions` em cada operação.
4. Implementar regra de expiração reativa (jornada > 24h → expired; token expirado → 410).
5. Testar fluxo completo via Swagger simulando o cenário da Ana manualmente (uma call por vez).

### Dia 3 — Chat simulado + App simulado

**Meta ao fim do dia:** cliente consegue conversar no chat, receber o link, abrir o App e ver os dados preservados. **Cenário do caminho feliz funcionando ponta a ponta no navegador.**

1. Criar a página do chat simulado (`channels/whatsapp-sim/`) — HTML/CSS/JS puro.
2. Implementar máquina de estados do bot (JS no cliente) + chamadas à API do CFE a cada etapa.
3. Adicionar badges internos do CFE na UI do chat.
4. Criar a página do App simulado (`channels/minha-claro-app/`) — tela de login + tela de confirmação de plano.
5. Implementar resolução de token no App e renderização com dados preservados.
6. Implementar botão "Confirmar" que fecha a jornada com `concluded`.

### Dia 4 — Painel do atendente + refinamento do fluxo

**Meta ao fim do dia:** atendente consegue buscar cliente, ver histórico completo, painel atualiza sozinho. **Cenário 2 (Carlos com escalada humana) funcional.**

1. Criar página do painel (`channels/attendant-panel/`).
2. Implementar busca por CPF/telefone → resolve identidade → busca contexto ativo.
3. Renderizar bloco de dados, status e timeline de histórico.
4. Implementar polling (a cada 3-5s).
5. Testar cenário do Carlos: iniciar no chat, gerar link, cliente NÃO conclui, atendente busca no painel enquanto isso, vê tudo em tempo real.

### Dia 5 — Polimento + testes + demo

**Meta ao fim do dia:** README funcional, os 3 cenários testados manualmente, demo ensaiada.

1. Configurar Serilog (se ainda não estiver).
2. Adicionar tratamento de erros consistente (respostas JSON padronizadas para 400/404/409/410).
3. Testar cenário 3 (Mariana) — pode ser simulado manipulando a data de `updated_at` diretamente no banco (para não esperar 24h reais).
4. Escrever README com: pré-requisitos, como rodar (docker-compose up + dotnet run + abrir URLs), URLs de cada canal, roteiro sugerido de demo.
5. Fazer uma passada de CSS pra não ficar constrangedor.
6. Ensaiar a demo pelo menos uma vez.
7. **Buffer:** se sobrar tempo, entrar em COULD.

---

## 11. Boas práticas de desenvolvimento

### 11.1. Trabalhe em fatias verticais

Prefira sempre terminar **uma fatia ponta a ponta** (API + banco + teste) antes de partir pra próxima. É melhor ter Identity 100% pronto no Dia 1 do que Identity + Context + Handoff 50% cada. Uma fatia completa é demonstrável; três fatias parciais não são.

### 11.2. Commit atômico e frequente

Faça commits pequenos, com mensagem clara em inglês: `feat(identity): add resolve endpoint`, `chore(db): initial migration`, `fix(context): return 409 when updating closed journey`. Isso te salva se algo quebrar e você precisar reverter.

### 11.3. Teste manual via Swagger antes de codar o front

Todos os endpoints devem ser testáveis via Swagger antes de qualquer front ser conectado. Isso separa problemas de backend de problemas de UI e economiza muito tempo de debug.

### 11.4. Não confie em memória — use o banco

Estado do bot pode ser mantido em memória (dicionário indexado por session_id) apenas para simplicidade **do canal simulado**; o CFE em si NUNCA guarda estado da jornada em memória, sempre no banco. Isso é fundamental para o handoff funcionar entre canais.

### 11.5. Erros ricos, não genéricos

Sempre retorne resposta JSON com `error_code`, `message` e (quando útil) `details`. Ex:

```json
{ "error_code": "invalid_cpf", "message": "CPF deve conter 11 dígitos numéricos." }
```

### 11.6. Idempotência onde possível

Endpoints que criam recursos devem tolerar retentativa. Se `open` for chamado duas vezes com os mesmos dados, retorne a jornada existente ao invés de duplicar.

### 11.7. Não esconda o CFE na demo

O grande diferencial do projeto é o CFE. Deixe visível na UI (badges no chat, banner no App, aviso no painel) que o CFE está atuando. Isso torna a demonstração autoexplicativa.

### 11.8. Nunca hardcode segredos

Connection string do banco vai em `appsettings.Development.json` (com .gitignore) ou variáveis de ambiente. Nem que seja postgres local, adote o hábito.

### 11.9. Documente decisões pequenas no código

Comentário curto ao lado de decisões não óbvias ajuda muito depois:
```csharp
// Sessão do bot em memória — canal simulado, aceitável para o MVP.
// Em produção, isso deve ir para Redis ou similar.
```

### 11.10. Prefira convenções REST

- `POST /resource` cria
- `GET /resource/{id}` lê um
- `PATCH /resource/{id}` atualiza parcial
- `POST /resource/{id}/action` para ações que não se encaixam em CRUD
- Status codes corretos: 200/201/204/400/404/409/410/500

### 11.11. Simule o adversário na hora de testar

Ao terminar cada fatia, tente quebrar: CPF sem número, token com caracteres estranhos, atualização em jornada já fechada, deep link com token vazio. Não é sobre passar em teste unitário — é sobre garantir que a demo aguenta perguntas ao vivo da banca.

---

## 12. Critérios de aceite (Definition of Done funcional)

O protótipo será considerado pronto quando **todos** os critérios abaixo forem verdadeiros:

- [ ] Consigo iniciar uma jornada no chat simulado com um CPF de cliente existente no seed, e a jornada é criada no banco com todos os campos corretos.
- [ ] Consigo iniciar uma jornada no chat simulado com um CPF novo, o bot me pergunta o nome, e o cliente é registrado corretamente.
- [ ] O CPF inválido é rejeitado com mensagem clara.
- [ ] Ao selecionar um plano, o `payload` da jornada é atualizado e a transição é registrada.
- [ ] O deep link é gerado com token válido e URL que abre o App.
- [ ] O App resolve o token, exibe dados preservados, e o cliente pode confirmar ou cancelar.
- [ ] O botão "Confirmar" fecha a jornada com `concluded`; "Cancelar" fecha com `abandoned`.
- [ ] Um token expirado (>30min) ou já usado retorna 410, e o App exibe tela de sessão expirada.
- [ ] O painel busca por CPF e mostra o cliente, o status da jornada e o histórico completo.
- [ ] O painel atualiza sozinho a cada 3-5 segundos enquanto a jornada avança em outro canal (comprovável).
- [ ] Uma jornada com `updated_at` manipulado para >24h atrás é retornada como `expired` na próxima consulta.
- [ ] Toda operação do CFE gera entrada em `journey_transitions`.
- [ ] `/health` responde 200 com status OK.
- [ ] README explica como rodar tudo do zero em <10 minutos.
- [ ] Os 3 cenários do Sprint 2 (Ana, Carlos, Mariana) são testáveis manualmente e funcionam.

---

## 13. Cenários de validação (usar como roteiros de teste)

Esses cenários **não são o produto**, são checklists para validar que o sistema genérico funciona corretamente.

### Cenário 1 — Caminho feliz (Ana)

1. Abrir chat. Bot cumprimenta.
2. Enviar "quero trocar de plano".
3. Bot pergunta CPF. Enviar `12345678900` (cliente do seed).
4. Bot confirma identidade. Badge do CFE aparece.
5. Bot mostra planos. Escolher "60GB".
6. Bot envia deep link.
7. Clicar no link → App abre → login mockado → tela de confirmação com nome, CPF, plano atual, novo plano.
8. Clicar "Confirmar troca" → jornada fecha com `concluded`.

**Validação:** verificar no banco que jornada tem `status=concluded`, e histórico tem no mínimo 6 transições.

### Cenário 2 — Escalada humana (Carlos)

1. Repetir passos 1-6 com outro CPF (`98765432100`).
2. NÃO clicar no link. Deixar aberto.
3. Abrir painel em outra aba.
4. Buscar por `98765432100`.
5. Ver status "em andamento", canal atual "WhatsApp", última ação recente.
6. Enquanto isso, voltar ao chat, clicar no link, chegar até a tela do App.
7. Voltar ao painel. **Sem recarregar**, o histórico deve ter atualizado sozinho mostrando "Jornada retomada no App Minha Claro" (via polling).

**Validação:** o polling captura mudança automaticamente.

### Cenário 3 — Abandono e expiração (Mariana)

1. Repetir passos 1-6 com outro CPF (`45678912300`).
2. NÃO clicar no link.
3. Via SQL, manipular `updated_at` da jornada para `NOW() - INTERVAL '25 hours'`.
4. Clicar no link agora → App tenta resolver → CFE retorna 410 "expired".
5. App exibe tela de sessão expirada.
6. Verificar no banco que jornada mudou para `status=expired` e transição `journey_expired` foi registrada.

**Validação:** regra reativa funciona.

---

## 14. Fora do escopo do protótipo (roadmap para o documento de visão)

Para deixar claro nas próximas iterações e na apresentação final, os itens abaixo ficam explicitamente para evolução futura:

- Autenticação real por JWT/token de serviço entre canais e CFE.
- Login e perfis de acesso no painel do atendente.
- Job de background para expiração ativa (fora do modelo reativo).
- Integração com WhatsApp Business API (via 360dialog/Twilio/Meta Cloud API) ou Telegram Bot API.
- Outros canais: Alexa, RCS, SMS, USSD, Site, Portal Cautivo, Totem, Dial My App, AppBot.
- Outras intenções: 2ª via de fatura, negociação, suporte técnico, aquisição de produto.
- Métricas operacionais no painel: TMA, taxa de handoff, taxa de abandono.
- Notificação automática para o time técnico via webhook/e-mail em degradação.
- Extração dos módulos internos para microsserviços independentes.
- Painel em React (ou reescrita da UI para stack moderno).
- LGPD: implementação completa de retenção, exclusão sob solicitação e portabilidade de dados.
- Acessibilidade: integração com VLibras e adequação a WCAG 2.1 AA.

---

**Fim da Especificação Funcional.**
