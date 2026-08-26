# Claro Flow Engine (CFE)

Camada de orquestração conversacional que preserva contexto de jornada entre canais de atendimento da Claro

![MVP funcional](https://img.shields.io/badge/MVP-funcional-brightgreen) ![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![PostgreSQL 16](https://img.shields.io/badge/PostgreSQL-16-336791) ![Docker](https://img.shields.io/badge/Docker-ready-2496ED) ![Challenge FIAP × Claro 2026](https://img.shields.io/badge/Challenge-FIAP%20%C3%97%20Claro%202026-E30613) ![Licença acadêmica](https://img.shields.io/badge/licen%C3%A7a-acad%C3%AAmica-lightgrey)

Protótipo funcional desenvolvido para o Challenge FIAP × Claro 2026, pela Equipe Horizon — Turma 4SI FIAP.

---

## Quickstart

```bash
git clone https://github.com/givasques/Challenge.ClaroFlowEngineCFE.git
cd Challenge.ClaroFlowEngineCFE
docker compose -f docker-compose.full.yml up
```

Acesse **http://localhost:5104/channels/whatsapp-sim/** para começar.

---

## Sobre o projeto

Clientes de operadoras costumam iniciar um atendimento em um canal (ex: WhatsApp) e precisar continuá-lo em outro (ex: o app oficial, ou com um atendente humano) — e, na maioria dos sistemas, isso significa recomeçar do zero: reinformar CPF, reexplicar o problema, refazer escolhas já feitas. O CFE existe para resolver essa descontinuidade.

A solução é uma camada de orquestração posicionada entre os canais e o backend: ela resolve a identidade unificada do cliente independentemente do identificador usado em cada canal, persiste o contexto da jornada (intenção, etapa, dados já coletados) em tempo real, e permite transferir essa jornada de um canal para outro por meio de um deep link com token — sem reintrodução de dados.

Este repositório contém um **protótipo funcional**, não um produto de produção: dados de clientes e planos são mockados (seed automático), a autenticação por canal é simulada via header, e os três canais de atendimento (chat, app, painel do atendente) são interfaces simuladas construídas para o protótipo, não integrações reais com WhatsApp Business API ou um app publicado.

---

## Integrantes — Equipe Horizon — Turma 4SI FIAP

<!-- preencher com integrantes do time -->

| Nome | RM | GitHub | LinkedIn |
|---|---|---|---|
| | | | |
| | | | |
| | | | |

---

## Screenshots

<!-- ![Chat WhatsApp simulado](docs/screenshots/chat.png) -->
<!-- ![App Minha Claro simulado](docs/screenshots/app.png) -->
<!-- ![Painel do Atendente](docs/screenshots/painel.png) -->

*(prints ainda não adicionados — ver [`docs/screenshots/`](docs/screenshots/))*

---

## Arquitetura

```
┌──────────────┐   ┌──────────────┐   ┌──────────────────┐
│  Chat         │   │  App          │   │  Painel do        │
│  WhatsApp     │   │  Minha Claro  │   │  Atendente        │
│  (simulado)   │   │  (simulado)   │   │  (simulado)       │
└──────┬───────┘   └──────┬───────┘   └─────────┬─────────┘
      │                   │                      │
      │      HTTP/JSON — header X-Channel-Token  │
      └───────────────────┼──────────────────────┘
                           ▼
            ┌──────────────────────────┐
            │   Claro Flow Engine API   │
            │  ┌─────────┬───────────┐  │
            │  │Identity │ Context   │  │
            │  ├─────────┼───────────┤  │
            │  │      Handoff        │  │
            │  └──────────────────────┘  │
            └────────────┬──────────────┘
                           ▼
                  ┌─────────────────┐
                  │   PostgreSQL 16  │
                  └─────────────────┘
```

- **Identity** — resolve a identidade unificada do cliente a partir de qualquer identificador (CPF, telefone, login).
- **Context** — mantém o ciclo de vida da jornada (abertura, atualização, expiração, encerramento) e o histórico de transições.
- **Handoff** — gera e resolve os tokens de deep link que transferem uma jornada entre canais.

Os três módulos rodam num único processo (monolito modular) — ver [Decisões arquiteturais](#decisões-arquiteturais).

---

## Setup detalhado

### Pré-requisitos

- **Docker + Docker Compose** — obrigatório em ambos os modos abaixo.
- **.NET 10 SDK** — só necessário no modo desenvolvimento.
- **Node.js** (para `npx http-server`) — só necessário no modo desenvolvimento, para servir os canais simulados.

### Modo desenvolvimento

Uso: dia a dia de código, hot reload, debug. API roda via `dotnet run` na porta **5104**; cada canal roda numa porta própria (5171/5173/5175).

```bash
docker compose up -d

cd src/ClaroFlowEngine.Api
dotnet restore
dotnet run
```

Migrations e seed rodam automaticamente. Configure antes `appsettings.Development.json` (não versionado — exemplo em `specs/v2-modo-explicacao/spec-tecnica.md §9.2.2`). Swagger disponível em `http://localhost:5104/swagger` **neste modo** (habilitado só em ambiente `Development`).

Em três terminais separados, sirva os canais:

```bash
npx http-server channels/whatsapp-sim -p 5171 -c-1
npx http-server channels/minha-claro-app -p 5173 -c-1
npx http-server channels/attendant-panel -p 5175 -c-1
```

### Modo full (Docker Compose completo)

Uso: testar/demonstrar tudo funcionando do zero, sem instalar .NET/Node localmente. Sobe Postgres **e** API juntos; a própria API serve os três canais em `http://localhost:5104/channels/<canal>/`.

```bash
docker compose -f docker-compose.full.yml up --build
```

Só inicia a API depois que o Postgres está pronto (`depends_on: condition: service_healthy`). Este modo roda em ambiente `Staging` (não `Production`, para permitir migration/seed automáticos; não `Development`, então **o Swagger não fica disponível aqui** — só no modo desenvolvimento). Detalhes completos em `specs/v2-modo-explicacao/spec-tecnica.md §9.3`.

```bash
# derrubar mantendo os dados
docker compose -f docker-compose.full.yml down

# derrubar e apagar o volume do banco
docker compose -f docker-compose.full.yml down -v
```

⚠️ Não rode os dois modos ao mesmo tempo sem ajustar portas — cada um sobe seu próprio container de Postgres.

### Testes

O projeto não tem suíte de testes automatizados. Testes manuais estruturados (caminho feliz + caminhos de erro) foram executados a cada fase de desenvolvimento — ver relatórios em `specs/relatorios/` (uso interno, não versionado neste repositório).

### Estrutura do repositório

```
Challenge.ClaroFlowEngineCFE/
├── docker-compose.yml        # só o Postgres — modo desenvolvimento
├── docker-compose.full.yml   # Postgres + API — modo full
├── src/
│   └── ClaroFlowEngine.Api/
│       ├── Dockerfile
│       ├── Modules/           # Identity, Context, Handoff (feature folders)
│       ├── Data/               # entidades, migrations, seed
│       └── Common/             # middleware, erros, serviços compartilhados
├── channels/
│   ├── whatsapp-sim/          # chat simulado
│   ├── minha-claro-app/       # App simulado
│   └── attendant-panel/       # painel do atendente
├── docs/
│   └── screenshots/            # prints das telas (ver acima)
└── specs/                     # especificações funcional, técnica e de padrões
```

---

## Roteiros de demonstração

Os três clientes de teste já vêm no seed automático. Com a stack rodando, abra o chat, o App e o painel em abas separadas.

### Cenário 1 — Caminho feliz (Ana Silva, CPF `12345678900`) · ~2 min

1. No chat, diga algo como "quero trocar de plano".
2. Informe o CPF `12345678900` quando pedido.
3. Escolha um plano (ex: "60GB") quando o bot listar as opções.
4. Clique no botão "Continuar no App" do card que aparece.
5. No App, faça login com qualquer usuário/senha e confirme a troca.
6. Verifique no painel (buscando `12345678900`) que a jornada aparece como "Concluída".

### Cenário 2 — Escalada humana (Carlos Mendes, CPF `98765432100`) · ~3 min

1. Repita os passos 1-3 do cenário 1 com o CPF `98765432100`.
2. **Não** clique no link do card.
3. Abra o painel em outra aba e busque `98765432100` — deve aparecer "Em andamento".
4. Volte ao chat, clique no link, abra o App, mas não confirme ainda.
5. Volte ao painel **sem recarregar a página** — em até 4 segundos, o histórico deve mostrar "Jornada retomada em outro canal" sozinho (polling).

### Cenário 3 — Abandono e expiração (Mariana Souza, CPF `45678912300`) · ~2 min

1. Repita os passos 1-3 do cenário 1 com o CPF `45678912300`.
2. **Não** clique no link.
3. Force a expiração via SQL (ajuste o container conforme o modo usado):
   ```sql
   UPDATE journey_contexts
   SET updated_at = NOW() - INTERVAL '25 hours'
   WHERE customer_id = (SELECT id FROM customers WHERE cpf = '45678912300') AND status = 'open';
   ```
4. Clique no link do chat agora — o App deve mostrar a tela de "Sessão expirada".

### Cenário 4 (opcional) — Degradação de canal · ~2 min

1. Inicie uma conversa no chat até a etapa de escolha de plano.
2. Pare o container/processo da API (`docker stop cfe-api-full` no modo full, ou `Ctrl+C` no `dotnet run` em dev).
3. Envie a escolha do plano — o chat deve avisar sobre a instabilidade, sem travar.
4. No painel (se estiver com uma jornada aberta), a mesma indisponibilidade deve aparecer como uma faixa de aviso, mantendo os últimos dados carregados visíveis.
5. Suba a API de novo e repita o envio — deve funcionar normalmente.

---

## Mapeamento de requisitos

A spec funcional deste projeto organiza os requisitos como casos de uso (UC01–UC09), não como uma lista numerada de RF/RNF — a tabela abaixo segue essa mesma estrutura.

| Caso de uso | Descrição | Implementação |
|---|---|---|
| UC01 | Iniciar jornada | `POST /context/open` + máquina de estados do chat |
| UC02 | Resolver identidade unificada | `POST` / `GET /identity/resolve` |
| UC03 | Registrar novo cliente | `POST /identity/resolve` com `full_name_hint` |
| UC04 | Atualizar contexto de jornada | `PATCH /context/{id}` |
| UC05 | Gerar deep link para handoff | `POST /handoff/generate` |
| UC06 | Retomar jornada em outro canal | `GET /context/resolve?token=` |
| UC07 | Encerrar jornada | `POST /context/{id}/close` |
| UC08 | Expirar jornada por inatividade | Verificação reativa em todo acesso a uma jornada aberta (`IJourneyExpirationService`) |
| UC09 | Consultar histórico de jornada (painel) | `GET /context/customer/{id}` + `GET /context/{id}/transitions`, com polling |
| RNF003 | Operação em modo degradado quando o CFE está indisponível | Timeout + retry + banner de indisponibilidade nos 3 canais |

Detalhamento completo de cada caso de uso (atores, pré-condições, fluxos alternativos) em `specs/v2-modo-explicacao/spec-funcional.md §5`.

---

## Decisões arquiteturais

**Monolito modular em vez de microsserviços.** Para o protótipo, um único processo com módulos isolados (Identity, Context, Handoff) entrega a mesma separação de responsabilidades sem o custo operacional de orquestrar múltiplos serviços, rede entre eles e deploy distribuído — desnecessário para validar a proposta de valor.

**PostgreSQL.** Suporte robusto a `JSONB` (usado para o payload flexível da jornada, que varia por intenção), maturidade, e zero custo de licenciamento — adequado tanto ao protótipo quanto a uma eventual evolução para produção.

**Chat web próprio em vez de WhatsApp Business API/Telegram.** Integrar uma API de mensageria real exigiria homologação, custos e credenciais fora do controle do time, sem agregar validação à proposta central (orquestração de contexto) — um chat simulado testa exatamente a mesma lógica de backend.

**Bot baseado em máquina de estados sem NLP.** A intenção do protótipo é validar a persistência e recuperação de contexto entre canais, não construir um motor de compreensão de linguagem natural — uma heurística de palavras-chave é suficiente para conduzir os cenários de demonstração de forma previsível.

---

## Limitações conhecidas

- O bot do chat reconhece intenção por heurística de palavras-chave, não por NLP real.
- A autenticação por canal (`X-Channel-Token`) é mockada via header — não é autenticação real (JWT, OAuth2 etc.), documentado como tal no próprio código.
- O login do App é mock: aceita qualquer credencial que atenda a um formato mínimo, sem verificação contra base real.
- Não há cobertura de testes automatizados; a validação é manual e estruturada, uma fase por vez.
- A regra de expiração de jornada é reativa (verificada no momento do acesso), não um job agendado em background.
- Campos do painel do atendente como "segmento" e "vencimento" são placeholders visuais — não há dado real correspondente no modelo atual.
- Os três canais simulados não têm build step nem framework de frontend: HTML/CSS/JS puro, sem testes de UI automatizados.

---

## Documentação complementar

Este README aponta para a documentação completa — o conteúdo detalhado vive em `specs/`, não é duplicado aqui:

- [`specs/v2-modo-explicacao/spec-funcional.md`](specs/v2-modo-explicacao/spec-funcional.md) — o que o sistema faz: casos de uso, regras de negócio, máquinas de estado.
- [`specs/v2-modo-explicacao/spec-tecnica.md`](specs/v2-modo-explicacao/spec-tecnica.md) — como o sistema é construído: stack, contratos de API, modelagem de dados, setup.
- [`specs/padroes-e-boas-praticas.md`](specs/padroes-e-boas-praticas.md) — convenções de código, nomenclatura, padrões de commit.
- [`specs/v2-modo-explicacao/spec-modo-explicacao.md`](specs/v2-modo-explicacao/spec-modo-explicacao.md) — modo explicação (painel de orquestração para apresentação didática) — **planejado, não implementado**.
- `specs/relatorios/` — resumo de cada fase de desenvolvimento (uso interno, não versionado neste repositório).

---

## Roadmap de evolução

### Próximo passo planejado — ETAPA 2

Refinamento do protótipo em quatro frentes:

- **(0) Housekeeping** — correção de bugs conhecidos e limpezas pontuais identificadas ao longo do desenvolvimento.
- **(A) Bot com interativos** — evolução do chat simulado para suportar elementos de interação estruturada (ex: botões/listas), além do texto livre atual.
- **(B) Painel enriquecido** — dados adicionais e recursos operacionais no painel do atendente, além do que existe hoje.
- **(C) Intenção "contestação de cobrança indevida"** — segunda intenção suportada pelo CFE, além de troca de plano, validando a genericidade da arquitetura de contexto.

### Depois da ETAPA 2

**Modo Explicação** — um painel de orquestração que pausa a execução do CFE em pontos-chave e exibe, em tempo real, qual componente está processando o quê — pensado para tornar a demonstração didática, já que o protótipo em operação normal executa em menos de um segundo por requisição. Especificado em `specs/v2-modo-explicacao/spec-modo-explicacao.md`, ainda não implementado.

### Evoluções futuras possíveis

Sem compromisso de prazo — dependem de uma eventual evolução do protótipo para produto:

- Autenticação real entre canais e API (JWT/OAuth2), substituindo o header mockado.
- Expansão para outros canais (Alexa, RCS, SMS, USSD, totem) e outras intenções além de troca de plano.
- Notificação automática ao time técnico em caso de indisponibilidade prolongada.
- Acessibilidade (WCAG 2.1 AA, integração com VLibras).
- Extração dos módulos internos para microsserviços independentes, se a escala justificar.
- Painel do atendente com métricas operacionais (TMA, taxa de abandono) e reescrita em stack moderno (React/Vue).
- Cobertura ampliada de LGPD (exercício de direitos do titular, anonimização automática).
- Integração real com WhatsApp Business API.

Detalhamento completo em `specs/v2-modo-explicacao/spec-funcional.md §15`.

---

## Licença

Projeto acadêmico desenvolvido para o Challenge FIAP × Claro 2026. Todos os dados são fictícios; nomes de produtos são referências acadêmicas.
