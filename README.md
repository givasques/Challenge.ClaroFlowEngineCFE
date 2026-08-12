# Claro Flow Engine (CFE)

Protótipo funcional de uma camada de orquestração conversacional entre canais de atendimento da Claro — garante continuidade de jornada quando o cliente começa em um canal (ex: WhatsApp) e continua em outro (ex: App Minha Claro), sem repetir informações.

Projeto do time **Horizon** (FIAP 4SI / Challenge Claro 2026). Especificações completas em [`specs/`](specs/).

---

## Pré-requisitos

- **Docker + Docker Compose** — obrigatório em ambos os modos abaixo.
- **.NET SDK** (8 LTS documentado na spec original; o protótipo foi implementado com .NET 10) — só necessário no **modo desenvolvimento**.
- **Node.js** (para `npx http-server`) — só necessário no **modo desenvolvimento**, para servir os canais simulados.

---

## Dois modos de execução

O projeto pode ser rodado de duas formas, dependendo do que você precisa fazer:

| | **Desenvolvimento** | **Full (Docker)** |
|---|---|---|
| **Quando usar** | Dia a dia de código — hot reload, debug, iterar rápido no backend | Testar/demonstrar tudo funcionando do zero, sem instalar .NET/Node localmente |
| **O que sobe** | Só o Postgres via Docker; API via `dotnet run`; canais via `http-server` | Postgres **e** API via Docker Compose (API também serve os canais) |
| **Arquivo** | `docker-compose.yml` | `docker-compose.full.yml` |
| **URLs dos canais** | Uma porta por canal (5171/5173/5175) | Todas sob `http://localhost:5104/channels/<canal>` |

⚠️ **Não rode os dois modos ao mesmo tempo** sem ajustar portas — cada um sobe seu próprio container de Postgres, com nomes e volumes diferentes, mas isso normalmente não é necessário.

---

## Modo desenvolvimento

1. Suba o banco:
   ```bash
   docker compose up -d
   ```

2. Configure `src/ClaroFlowEngine.Api/appsettings.Development.json` (não versionado — copie o exemplo documentado em `specs/v2-modo-explicacao/spec-tecnica.md §9.2.2`, ajustando a porta do Postgres se necessário).

3. Rode a API:
   ```bash
   cd src/ClaroFlowEngine.Api
   dotnet run
   ```
   Migrations e seed rodam automaticamente. API disponível em `http://localhost:5104` (Swagger em `/swagger`, health check em `/health`).

4. Em três terminais separados, sirva os canais simulados:
   ```bash
   npx http-server channels/whatsapp-sim -p 5171 -c-1
   npx http-server channels/minha-claro-app -p 5173 -c-1
   npx http-server channels/attendant-panel -p 5175 -c-1
   ```

---

## Modo full (Docker Compose completo)

Sobe tudo com um único comando — útil para validar que o projeto funciona "do zero" numa máquina limpa, ou para demonstração:

```bash
docker compose -f docker-compose.full.yml up --build
```

Isso builda a imagem da API (multi-stage: SDK para compilar, ASP.NET Core Runtime para rodar) a partir de `src/ClaroFlowEngine.Api/Dockerfile`, sobe o Postgres com healthcheck, e só inicia a API depois que o banco está pronto para aceitar conexões (`depends_on: condition: service_healthy`).

Depois de subir:

- API: `http://localhost:5104` (Swagger em `/swagger`, health check em `/health`)
- Chat WhatsApp simulado: `http://localhost:5104/channels/whatsapp-sim/`
- App Minha Claro simulado: `http://localhost:5104/channels/minha-claro-app/`
- Painel do atendente: `http://localhost:5104/channels/attendant-panel/`

Para derrubar:

```bash
# mantendo os dados do banco
docker compose -f docker-compose.full.yml down

# apagando também o volume do banco
docker compose -f docker-compose.full.yml down -v
```

Detalhes técnicos completos (por que `Staging` em vez de `Production`, por que portas diferentes, etc.) estão documentados em `specs/v2-modo-explicacao/spec-tecnica.md §9.3`.

---

## Estrutura do repositório

```
ClaroFlowEngine/
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
└── specs/                     # especificações funcional, técnica e de padrões
```

---

## Roteiro de demonstração

Os três clientes de teste já vêm no seed automático. Com a stack rodando (qualquer um dos dois modos), abra o chat, o App e o painel em abas separadas e siga um dos roteiros abaixo.

### Cenário 1 — Caminho feliz (Ana Silva, CPF `12345678900`)

1. No chat, diga algo como "quero trocar de plano".
2. Informe o CPF `12345678900` quando pedido.
3. Escolha um plano (ex: "60GB") quando o bot listar as opções.
4. Clique no botão "Continuar no App" do card que aparece.
5. No App, faça login com qualquer usuário/senha e confirme a troca.
6. Verifique no painel (buscando `12345678900`) que a jornada aparece como "Concluída".

### Cenário 2 — Escalada humana (Carlos Mendes, CPF `98765432100`)

1. Repita os passos 1-3 do cenário 1 com o CPF `98765432100`.
2. **Não** clique no link do card.
3. Abra o painel em outra aba e busque `98765432100` — deve aparecer "Em andamento".
4. Volte ao chat, clique no link, abra o App, mas não confirme ainda.
5. Volte ao painel **sem recarregar a página** — em até 4 segundos, o histórico deve mostrar "Jornada retomada em outro canal" sozinho (polling).

### Cenário 3 — Abandono e expiração (Mariana Souza, CPF `45678912300`)

1. Repita os passos 1-3 do cenário 1 com o CPF `45678912300`.
2. **Não** clique no link.
3. Force a expiração via SQL (ajuste o container conforme o modo usado):
   ```sql
   UPDATE journey_contexts
   SET updated_at = NOW() - INTERVAL '25 hours'
   WHERE customer_id = (SELECT id FROM customers WHERE cpf = '45678912300') AND status = 'open';
   ```
4. Clique no link do chat agora — o App deve mostrar a tela de "Sessão expirada".

### Cenário 4 (opcional) — Degradação de canal

1. Inicie uma conversa no chat até a etapa de escolha de plano.
2. Pare o container/processo da API (`docker stop cfe-api-full` no modo full, ou `Ctrl+C` no `dotnet run` em dev).
3. Envie a escolha do plano — o chat deve avisar sobre a instabilidade, sem travar.
4. No painel (se estiver com uma jornada aberta), a mesma indisponibilidade deve aparecer como uma faixa de aviso, mantendo os últimos dados carregados visíveis.
5. Suba a API de novo e repita o envio — deve funcionar normalmente.

---

## Status do projeto

Progresso documentado fase a fase em `specs/relatorios/` (uso interno, não versionado). Especificações de referência em `specs/v2-modo-explicacao/`.

---

**Fim do README.**
