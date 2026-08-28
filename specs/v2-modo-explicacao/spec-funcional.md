# Especificação Funcional — Claro Flow Engine (CFE)

**Projeto:** Claro Flow Engine (CFE) — Protótipo Funcional
**Time:** Horizon (FIAP 4SI / Challenge Claro 2026)
**Versão:** 1.1
**Público-alvo deste documento:** desenvolvedor(a) e/ou agente de IA responsável pela codificação do protótipo
**Complementado por:** spec-modo-explicacao.md (documentação do Painel de Orquestração e sistema de pausas didáticas para apresentação)

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
- **Painel de Orquestração** com Modo Explicação — 4ª interface web que expõe visualmente o funcionamento interno do CFE, com capacidade de pausar a execução em pontos-chave para explicações didáticas durante a apresentação. Detalhes completos em `spec-modo-explicacao.md`.
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
1. Sistema recebe um par `{channel, identifier}` (ex: `{whatsapp, "5511999998888"}`, `{cpf, "11144477735"}` ou `{app, "user_abc"}`).
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
- **Validação completa de CPF** (ETAPA 2, Passo 0, item 3.5): formato **e** dígitos verificadores, pelo algoritmo padrão brasileiro. CPFs com todos os dígitos iguais (`00000000000`, `11111111111`, etc.) são sempre rejeitados. Falha retorna `invalid_cpf` (400).
- Os CPFs do seed são gerados para passar na validação de dígitos verificadores (não correspondem a pessoas reais).

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

- No MVP, um cliente só pode ter **uma jornada com status `open`** simultânea (por cliente + intenção).
- Política adotada (ETAPA 2, Passo 0): `POST /context/open` é **sempre idempotente** — se já existe jornada `open` para o mesmo cliente + intenção, o sistema retorna a jornada existente sem criar uma nova, registrando a transição `journey_reopen_attempted`. O backend nunca encerra uma jornada anterior implicitamente ao receber uma nova tentativa de abertura.
- Canais que oferecem alguma forma de "reiniciar conversa" (ex: menu do chat WhatsApp) são responsáveis por encerrar explicitamente a jornada anterior via `POST /context/{id}/close` (`outcome: "abandoned"`) **antes** de abrir uma nova — nunca contornando isso apenas no estado local do canal.

### 6.7. Rastreabilidade de acessos

- Toda operação relevante gera uma linha em `journey_transitions`, com `event_type`, `channel`, `description` e `metadata` (JSON).
- Consultas do painel geram transição do tipo `panel_accessed`.
- **Deduplicação por tempo** (ETAPA 2, Passo B, item 5.5): uma nova `panel_accessed` só é registrada se a última, na mesma jornada, tiver ocorrido há mais de `PanelAccessDedupMinutes` (configurável, padrão 5 minutos). Evita inflar o histórico quando o atendente troca de aba e volta ao mesmo cliente repetidamente em uma curta janela de tempo.

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
- **Elementos interativos (ETAPA 2, Passo A):** onde faz sentido, o bot oferece **botões** (até 3 opções, decisões rápidas) ou uma **lista** (até 10 opções, com descrição) em vez de exigir texto livre — padrão real de bots do WhatsApp Business. Usado hoje na detecção de intenção (botão "Trocar de plano") e na escolha de plano (lista com nome e preço de cada plano). CPF, nome (cliente novo) e descrições livres continuam sendo sempre texto livre, por natureza. Uma camada oculta de heurística por palavras-chave continua aceitando texto livre residual equivalente à opção interativa (ex: digitar "quero trocar de plano" mesmo com o botão visível); texto que não corresponde a nenhuma opção é rejeitado com um pedido para usar as opções mostradas. Uma vez respondida (por clique ou por texto reconhecido), a mensagem interativa correspondente fica visualmente desabilitada e não pode ser respondida de novo.
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
  - **Bloco Dados do Cliente:** nome, CPF, telefone, plano atual, segmento (sempre "—", sem dado disponível). Desde a ETAPA 2 (Passo B), também: **cliente desde** (data de cadastro, exibida como "há X anos/meses" no destaque do topo e como data completa no detalhe) e **meio preferido** (canal de origem com mais jornadas do cliente; empate resolvido pelo mais recente).
  - **Bloco Resumo de interações:** total de jornadas do cliente, com quebra por desfecho (concluídas/abandonadas/expiradas) — ETAPA 2, Passo B. Oculto se o cliente não tem nenhuma jornada.
  - **Bloco Status da Jornada:** em andamento (com badge), canal de origem, canal atual, intenção, última ação (tempo relativo).
  - **Bloco Histórico da Jornada:** timeline da jornada **ativa**, ordenada da mais recente para a mais antiga, com ícone, título, descrição, canal e horário de cada transição.
  - **Bloco Histórico de jornadas anteriores** (ETAPA 2, Passo B): lista separada com as últimas jornadas **não ativas** do cliente (concluídas/abandonadas/expiradas — até `history_limit`, padrão 5), cada uma com status, intenção, canal de origem, última etapa e data relativa. Clicar num item expande e carrega (sob demanda) a timeline completa daquela jornada, no mesmo formato do bloco de histórico da jornada ativa. Aparece mesmo quando não há jornada ativa no momento.
- **Polling:** a cada 3-5 segundos, refaz as chamadas `GET /context/{id}` e `GET /context/{id}/transitions` para manter o painel atualizado em tempo real, **apenas para a jornada ativa** — jornadas anteriores não são re-buscadas automaticamente. Se detectar mudança, atualiza a UI sem recarregar a página.
- Aviso visível: "Este painel exibe o contexto de jornada em modo somente leitura. Nenhuma alteração pode ser feita aqui. Para atualizar dados do cliente, utilize o sistema CRM."

### 8.4. Comportamento em caso de indisponibilidade do CFE

Este comportamento materializa o requisito **RNF003** ("em caso de indisponibilidade momentânea, os canais devem manter operação em modo degradado") do Sprint 1.

Todos os canais devem tratar falhas de comunicação com o CFE (timeout, 5xx, indisponibilidade de rede) de forma explícita, **sem travar a interface**:

- **Chat simulado:** ao detectar falha na chamada ao CFE, o bot envia uma mensagem visível para o usuário: "Estou com uma instabilidade temporária no sistema. Vou continuar te atendendo, mas algumas informações podem levar mais tempo para serem processadas. Você pode tentar novamente em instantes." O input do usuário continua habilitado. Uma badge visual de degradação aparece no topo do chat: "⚠️ Modo degradado — CFE indisponível".
- **App simulado:** ao falhar em resolver o token do deep link, exibe tela explicativa: "Não conseguimos recuperar sua sessão neste momento. Você pode continuar acessando o App normalmente, mas os dados da sua conversa anterior não estão disponíveis. Tente novamente em alguns instantes." Botão para retentar a resolução.
- **Painel do atendente:** ao falhar no polling, exibe faixa de aviso: "⚠️ Sistema de contexto indisponível — os dados exibidos podem estar defasados. Última atualização: {timestamp}." Os dados últimos carregados permanecem visíveis para não deixar o atendente sem informação.

**Implementação técnica:**

- Timeout de HTTP em cada canal: 10 segundos por chamada (ou 6 minutos quando em modo explicação).
- Retry automático apenas em GETs: 1 retentativa após 2 segundos.
- POSTs/PATCHes não retentam automaticamente (evita duplicação); usuário reenvia manualmente.
- Todos os erros de comunicação são exibidos em UI, nunca escondidos no console.

**Como testar:** parar o container do backend (`docker compose stop` do serviço da API) e verificar que os três canais reagem conforme descrito.

---

## 9. Priorização (MoSCoW)

### MUST (sem isso não há entrega)

- API .NET com os 3 módulos internos (Identity, Context, Handoff) e todos os endpoints listados na spec técnica.
- PostgreSQL com schema completo + seed de dados mockados (clientes, planos).
- Chat simulado com máquina de estados para a intenção "troca de plano".
- App simulado com resolução de deep link e tela de confirmação.
- Painel do atendente com busca por CPF/telefone, exibição do contexto e histórico, e polling.
- **Painel de Orquestração com Modo Explicação** — capacidade de pausar execução em pontos-chave, mostrar snapshots de cada operação e continuar sob comando. Ver `spec-modo-explicacao.md`.
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

## 10. Ordem de desenvolvimento preferencial (7-8 dias)

A ordem abaixo garante que ao final de cada dia haja um **artefato funcional testável**, mesmo que incompleto. Nunca comece o dia N+1 sem ter terminado o objetivo funcional do dia N — é preferível ter os primeiros 4 dias 100% do que 8 dias 60%.

O cronograma foi estruturado em duas fases:
- **Fase 1 (Dias 1-4):** MVP funcional completo. Se algo der errado depois, você ainda tem um sistema demonstrável.
- **Fase 2 (Dias 5-8):** Modo Explicação e polimento didático.

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

**Meta ao fim do dia:** atendente consegue buscar cliente, ver histórico completo, painel atualiza sozinho. **Cenário 2 (Carlos com escalada humana) funcional.** MVP fechado.

1. Criar página do painel (`channels/attendant-panel/`).
2. Implementar busca por CPF/telefone → resolve identidade → busca contexto ativo.
3. Renderizar bloco de dados, status e timeline de histórico.
4. Implementar polling (a cada 3-5s).
5. Testar cenário do Carlos: iniciar no chat, gerar link, cliente NÃO conclui, atendente busca no painel enquanto isso, vê tudo em tempo real.
6. **Snapshot do progresso:** com o final do Dia 4 você tem um MVP demonstrável. Faça commit marcando esse ponto (`v1-mvp-funcional`).

---

**A partir daqui começa a Fase 2 — Modo Explicação.**

### Dia 5 — Backend do Explainer

**Meta ao fim do dia:** requisições dos canais com header `X-Explain-Session-Id` conseguem pausar em pontos-chave e serem liberadas via API, testável via curl.

1. Criar migration adicionando `explain_sessions` e `explain_steps`.
2. Implementar `IExplainService` + `ExplainService` (Singleton) com `TaskCompletionSource<bool>` para pausas.
3. Implementar `SnapshotBroadcaster` para SSE (ou polling fallback).
4. Criar `ExplainController` com endpoints de sessão, steps e stream.
5. Injetar `IExplainService` nos services de Identity, Context e Handoff.
6. Adicionar chamadas `PauseAsync` em todos os pontos definidos em `spec-modo-explicacao.md §3`.
7. Testar via Swagger + curl: criar sessão, disparar operação em outro terminal com header, verificar que pausa e libera corretamente.

### Dia 6 — Painel de Orquestração (frontend)

**Meta ao fim do dia:** painel de orquestração renderiza sessão em tempo real via SSE, com timeline, diagrama animado e botão continuar funcionando.

1. Criar `channels/orchestration-panel/` com HTML/CSS/JS.
2. Controles do cabeçalho: nova sessão, encerrar, seletor de granularidade.
3. Timeline vertical com scroll + estados visuais dos steps.
4. Diagrama SVG do CFE com highlight no componente ativo.
5. Coluna de detalhes com snapshot renderizado e botão continuar.
6. Conexão SSE + handlers para `step_waiting`, `step_continued`, `session_ended`.
7. Testar fluxo completo: iniciar sessão no painel, disparar operação em outro canal, ver step aparecendo, continuar, ver progressão.

### Dia 7 — Integração modo explicação nos canais

**Meta ao fim do dia:** os 3 canais existentes suportam ativação de modo explicação; toda a cadeia funciona ponta a ponta com pausas didáticas.

1. Adicionar controle "🎓 Explicar" no cabeçalho de cada canal (chat, App, painel do atendente).
2. Modal simples para colar `session_id`, salvar em `localStorage`.
3. Wrapper de `fetch` que injeta `X-Explain-Session-Id` quando ativo.
4. Aumentar timeout do fetch para 6 minutos nos canais.
5. Feedback visual durante pausa: "Aguardando explicação..." no chat, overlay no App, faixa no painel.
6. Faixa persistente indicando modo ativo + botão "Desativar".
7. Testar cada canal em modo explicação, ponta a ponta.

### Dia 8 — Polimento e ensaio final

**Meta ao fim do dia:** README completo, 3 cenários testados nos dois modos (normal e explicação), demo ensaiada.

1. Configurar Serilog estruturado se ainda não estiver.
2. Padronização de tratamento de erros.
3. Rodar os 3 cenários (Ana, Carlos, Mariana) em ambos os modos:
   - Modo normal — sem pausas, velocidade real.
   - Modo explicação — granularidade `high`, com pausas didáticas.
4. Ajustar snapshots verbosos ou pouco claros no modo explicação.
5. Testar cenário Mariana forçando `updated_at` no SQL.
6. Escrever README completo com: pré-requisitos, setup, URLs, roteiros dos dois modos de demo.
7. Passada final de CSS.
8. Ensaiar a apresentação com e sem pausas.
9. **Buffer:** se sobrar tempo, itens do COULD.

---

**Regra de contingência:** se algum dia da Fase 2 atrasar, o MVP da Fase 1 ainda é uma entrega defensável. Melhor um MVP polido do que um sistema com modo explicação inacabado.

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
{ "error_code": "invalid_cpf", "message": "CPF inválido — verifique os dígitos digitados." }
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
- [ ] Os 3 cenários principais do Sprint 2 (Ana, Carlos, Mariana) são testáveis manualmente e funcionam.
- [ ] O cenário 4 (degradação) foi validado ao menos uma vez: parar o backend e verificar que os canais não travam e exibem mensagens apropriadas.

---

## 13. Métricas de sucesso do MVP

Esta seção materializa o feedback recebido nos dois sprints anteriores sobre a necessidade de estabelecer **métricas mensuráveis do próprio protótipo**, em vez de apenas citar números de estudos de mercado.

As métricas abaixo são todas verificáveis pela própria arquitetura do CFE — não dependem de usuários reais, apenas de que o sistema funcione conforme especificado. Elas devem ser reportadas na apresentação final como evidências objetivas de que o protótipo cumpre o que a proposta prometeu.

### 13.1. Métricas primárias (o CFE está atacando o problema)

| Métrica | Meta | Como medir |
|---|---|---|
| **Taxa de zero-repetição na retomada de jornada** | 100% | Comparar campos preservados no `payload` da jornada entre chat e App. Nenhum campo já informado deve precisar ser reintroduzido. |
| **Taxa de sucesso do handoff com token válido** | 100% | Toda vez que um deep link válido (não expirado, não usado) for aberto no App, o contexto deve ser resolvido corretamente. |
| **Taxa de rastreabilidade das operações** | 100% | Toda operação sobre uma jornada deve gerar entrada em `journey_transitions`. Verificável por contagem: nº de operações de mutação = nº de linhas em `journey_transitions` para aquela jornada. |
| **Taxa de identidade unificada em canais distintos** | 100% | Após um cliente ser atendido no chat (canal `whatsapp`) e retomar no App (canal `app`), ambos os identificadores devem estar vinculados ao mesmo `unified_customer_id` na tabela `identity_links`. |

### 13.2. Métricas de comportamento correto (o CFE está tratando exceções)

| Métrica | Meta | Como medir |
|---|---|---|
| **Taxa de rejeição de token expirado** | 100% | Um token com `expires_at < NOW()` deve retornar 410 sempre. |
| **Taxa de rejeição de token já usado** | 100% | Um token com `used_at != NULL` deve retornar 410 sempre. |
| **Taxa de expiração correta de jornadas inativas** | 100% | Uma jornada com `updated_at > 24h` deve mudar para `status=expired` na próxima leitura. |
| **Rejeição de operação em jornada não-aberta** | 100% | `PATCH` ou `close` em jornada com status final deve retornar 409. |

### 13.3. Métricas de qualidade técnica

| Métrica | Meta | Como medir |
|---|---|---|
| **Cobertura dos 3 cenários do Sprint 2** | 3/3 | Ana, Carlos e Mariana devem ser reprodutíveis manualmente conforme roteiros da §14. |
| **Latência média das chamadas ao CFE** | < 200ms em ambiente local | Log estruturado registra `elapsed_ms` de cada request. |
| **Disponibilidade do endpoint de health** | 100% durante a demo | `/health` deve responder 200 em toda checagem. |
| **Ausência de erros não tratados** | 0 exceções 500 em fluxos previstos | Todo erro deve ter tratamento e retornar código apropriado (4xx/410). |

### 13.4. Métricas específicas do modo explicação

| Métrica | Meta | Como medir |
|---|---|---|
| **Cobertura dos pontos de pausa didáticos** | ≥ 25 pontos (granularidade `high`) | Contar chamadas a `PauseAsync` disparadas em uma execução completa do cenário 1. |
| **Sincronização em tempo real do painel** | < 500ms entre pausa e aparição no painel | Diferença entre `waiting_since` do step e recebimento do evento SSE no cliente. |

### 13.5. Métricas que ficariam em uma versão em produção (não medidas no protótipo)

Para deixar claro na apresentação que o time pensou na evolução, listamos aqui métricas que **exigem base real de usuários** e portanto não podem ser reportadas com dados mockados. Ficam como parte do roadmap:

- Redução do TMA (Tempo Médio de Atendimento) real.
- Taxa de handoff efetivamente utilizado por sessão.
- Taxa de abandono comparada com jornadas single-channel.
- CSAT / NPS antes e depois da implementação.
- Redução de custo operacional em canais humanos.

---

## 14. Cenários de validação (usar como roteiros de teste)

Esses cenários **não são o produto**, são checklists para validar que o sistema genérico funciona corretamente.

### Cenário 1 — Caminho feliz (Ana)

1. Abrir chat. Bot cumprimenta.
2. Enviar "quero trocar de plano".
3. Bot pergunta CPF. Enviar `11144477735` (cliente do seed).
4. Bot confirma identidade. Badge do CFE aparece.
5. Bot mostra planos. Escolher "60GB".
6. Bot envia deep link.
7. Clicar no link → App abre → login mockado → tela de confirmação com nome, CPF, plano atual, novo plano.
8. Clicar "Confirmar troca" → jornada fecha com `concluded`.

**Validação:** verificar no banco que jornada tem `status=concluded`, e histórico tem no mínimo **5 transições** (`journey_started`, `step_updated`, `deep_link_generated`, `journey_resumed`, `journey_closed`).

> **Nota (ETAPA 2, Passo 0, item 3.8):** a resolução de identidade (UC02) acontece antes da criação da jornada (UC01), portanto não gera entrada em `journey_transitions` — essa tabela exige FK obrigatória para a jornada. A auditoria da resolução de identidade fica registrada em logs estruturados (Serilog), não em transições de jornada.

### Cenário 2 — Escalada humana (Carlos)

1. Repetir passos 1-6 com outro CPF (`22255588846`).
2. NÃO clicar no link. Deixar aberto.
3. Abrir painel em outra aba.
4. Buscar por `22255588846`.
5. Ver status "em andamento", canal atual "WhatsApp", última ação recente.
6. Enquanto isso, voltar ao chat, clicar no link, chegar até a tela do App.
7. Voltar ao painel. **Sem recarregar**, o histórico deve ter atualizado sozinho mostrando "Jornada retomada no App Minha Claro" (via polling).

**Validação:** o polling captura mudança automaticamente.

### Cenário 3 — Abandono e expiração (Mariana)

1. Repetir passos 1-6 com outro CPF (`33366699957`).
2. NÃO clicar no link.
3. Via SQL, manipular `updated_at` da jornada para `NOW() - INTERVAL '25 hours'`.
4. Clicar no link agora → App tenta resolver → CFE retorna 410 "expired".
5. App exibe tela de sessão expirada.
6. Verificar no banco que jornada mudou para `status=expired` e transição `journey_expired` foi registrada.

**Validação:** regra reativa funciona.

### Cenário 4 (opcional) — Degradação de canal

1. Iniciar cenário 1 até o passo 5 (bot mostra planos).
2. Parar o container do backend: `docker compose stop api`.
3. Enviar a escolha do plano no chat.
4. Chat deve exibir mensagem de degradação, sem travar.
5. Painel do atendente aberto em outra aba deve exibir aviso de indisponibilidade.
6. Subir o backend novamente: `docker compose start api`.
7. Enviar a mesma escolha no chat — agora deve funcionar.

**Validação:** o RNF003 (operação degradada) foi implementado corretamente.

---

## 15. Roadmap de evolução

Esta seção consolida tudo que está **fora do escopo do protótipo** e materializa a resposta aos feedbacks dos sprints anteriores. Serve como referência para a apresentação final e para o documento de visão de evolução.

### 15.1. Autenticação e segurança de nível produção

- Substituição do `X-Channel-Token` mockado por autenticação real via JWT ou OAuth2 client credentials entre cada canal e o CFE.
- Login e perfis de acesso no painel do atendente, com identificação do atendente ativo e auditoria completa de acessos.
- Rate limiting por canal e por endpoint.
- HTTPS obrigatório com certificados válidos.
- Rotação automática de segredos via cofre (HashiCorp Vault, AWS Secrets Manager, Azure Key Vault).

### 15.2. Notificação de falhas para o time técnico da Claro (feedback direto do Sprint 1)

O feedback do professor questionou explicitamente sobre notificação técnica em RNF003. A evolução prevê:

- Integração com sistema de alertas (PagerDuty, Opsgenie, ou similar) para falhas críticas em produção.
- Notificação por webhook em degradação de performance (latência acima do SLA).
- E-mail automático ao time técnico em caso de indisponibilidade prolongada.
- Dashboards operacionais (Grafana + Prometheus) com métricas em tempo real.

### 15.3. Acessibilidade e inclusão (feedback direto do Sprint 1)

O feedback do professor questionou sobre clientes com necessidades especiais. A evolução prevê:

- Integração com a **API vLibras** (https://www.gov.br/conecta/catalogo/apis/vlibras) nos canais visuais para tradução automática para Língua Brasileira de Sinais.
- Adequação a **WCAG 2.1 nível AA** em todas as interfaces (contraste, navegação por teclado, leitores de tela, hierarquia semântica).
- Modo de alto contraste e ajuste de tamanho de fonte no App e no painel do atendente.
- Suporte a leitura de áudio nas mensagens do chat.

### 15.4. Expansão para outros canais

O MVP cobre WhatsApp e App Minha Claro. A camada de orquestração está preparada para receber, sem reformulação estrutural, os demais canais da Claro:

- **Alexa** (voz)
- **RCS** (mensagem enriquecida)
- **SMS** (mensagem simples)
- **USSD** (menu de discagem)
- **Site**
- **Portal Cautivo** (Wi-Fi público)
- **Totem** (autoatendimento presencial)
- **Dial My App** (transição telefone → app)
- **AppBot** (bot dentro do App)

Cada canal exige apenas a implementação de um adaptador que consuma as APIs REST do CFE, sem alteração no núcleo.

### 15.5. Expansão para outras intenções

O MVP cobre apenas "troca de plano". A camada de contexto suporta genericamente qualquer intenção. Evolução prevista:

- Segunda via de fatura
- Negociação de dívida
- Suporte técnico
- Aquisição de novo produto (linha adicional, TV, banda larga)
- Cancelamento com retenção
- Consulta de consumo
- Alteração de dados cadastrais

### 15.6. Extração para microsserviços

O monolito modular tem fronteiras arquiteturais desenhadas para permitir extração dos módulos internos como microsserviços independentes sem reescrita da lógica de negócio:

- Cada módulo (Identity, Context, Handoff) vira um serviço independente.
- Comunicação via message broker (RabbitMQ, Kafka) ou gRPC.
- Cada serviço com sua base de dados dedicada, seguindo o padrão de "database per service".
- Introdução de um API Gateway real (Kong, Ocelot, YARP) fazendo roteamento entre os serviços.

### 15.7. Painel do atendente enriquecido (feedback direto do Sprint 2)

O feedback pediu métricas operacionais no painel. Evolução prevista:

- **TMA** (Tempo Médio de Atendimento) por atendente e por período.
- **Taxa de handoff** por canal de origem.
- **Taxa de abandono** de jornadas antes da conclusão.
- **Lista de jornadas em andamento** com filtros por canal, intenção, status.
- **Histórico completo** de todas as jornadas do cliente (não apenas a ativa).
- **Ferramentas de ação:** capacidade de retomar jornada em nome do cliente, transferir para outro atendente, adicionar anotação.

### 15.8. Persistência do estado do bot no servidor

No MVP, o estado do bot conversacional é mantido em memória do cliente (localStorage). Evolução prevista:

- Sessão do bot persistida em Redis ou similar.
- Recuperação de sessão em caso de reload da página.
- Múltiplas sessões simultâneas do mesmo cliente em canais distintos.

### 15.9. Job de expiração ativa

No MVP, jornadas expiradas são detectadas apenas quando acessadas (regra reativa). Evolução prevista:

- Job de background (Hangfire, Quartz.NET) que varre `journey_contexts` a cada N minutos e marca inativas como `expired` proativamente.
- Notificação ao cliente antes da expiração ("Sua sessão expira em 1h — deseja continuar?").

### 15.10. Cobertura LGPD ampliada

O MVP já contempla auditabilidade e retenção. Evolução prevista:

- Endpoint para exercício de direitos do titular (acesso, correção, exclusão, portabilidade).
- Anonimização automática de campos sensíveis após período de retenção.
- Encryption at rest do banco.
- Log completo de acessos a dados pessoais, com dashboard de compliance.
- Consentimento explícito rastreável para uso de dados em canais específicos.

### 15.11. Integração real com WhatsApp e Telegram

O canal simulado atual é um chat web próprio. Evolução prevista:

- Integração com **WhatsApp Business API** via 360dialog, Twilio ou Meta Cloud API.
- Adaptador para **Telegram Bot API** como canal adicional de baixo custo para PMEs.
- Templates de mensagem homologados na plataforma da Meta.

### 15.12. Painel em stack moderno

O painel do atendente e o painel de orquestração foram construídos em HTML/CSS/JS puro por questão de tempo. Evolução prevista:

- Reescrita em **React** ou **Vue** com componentes reutilizáveis.
- Design system alinhado à identidade visual da Claro.
- SSR (Next.js) se SEO ou performance inicial forem relevantes.

### 15.13. Modo explicação em produção

O modo explicação atual é uma ferramenta didática para apresentação. Em produção, evolução prevista:

- Autenticação de admin obrigatória para iniciar sessão de explicação.
- Sanitização automática de dados sensíveis nos snapshots.
- Restrição por IP ou VPN.
- Uso apenas em ambientes de staging/homologação, não produção.
- Registro de auditoria específico para uso da ferramenta.

---

**Fim da Especificação Funcional.**
