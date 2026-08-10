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
   (os dois últimos ainda não implementados nas fases iniciais do protótipo.)

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
- App Minha Claro simulado: `http://localhost:5104/channels/minha-claro-app/` *(ainda não implementado)*
- Painel do atendente: `http://localhost:5104/channels/attendant-panel/` *(ainda não implementado)*

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
│   ├── whatsapp-sim/          # chat simulado (implementado)
│   ├── minha-claro-app/       # App simulado (pendente)
│   └── attendant-panel/       # painel do atendente (pendente)
└── specs/                     # especificações funcional, técnica e de padrões
```

## Status do projeto

Progresso documentado fase a fase em `specs/relatorios/` (uso interno, não versionado). Especificações de referência em `specs/v2-modo-explicacao/`.

---

**Fim do README.**
