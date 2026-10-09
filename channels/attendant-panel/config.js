// Configuração do painel do atendente.
//
// apiBaseUrl usa window.location.origin para funcionar tanto local (modo full) quanto em produção (Render),
// sem precisar de variáveis de build por ambiente — o canal é sempre servido pela própria API, então a
// origem da página já é o host correto.
// Exceção: modo dev isolado (dotnet run na 5104 + http-server por canal nas portas 5171/5173/5175), onde a
// página não é servida pela API — nesse caso apontamos explicitamente para localhost:5104.
//
// Não há mais channelToken fixo aqui (FASE 4.1, item C.1) — o painel autentica por login real,
// com JWT guardado em sessionStorage (ver app.js, getSession/setSession).
const IS_ISOLATED_DEV = ['5171', '5173', '5175'].includes(window.location.port);

const CFE_CONFIG = {
  apiBaseUrl: IS_ISOLATED_DEV ? 'http://localhost:5104' : window.location.origin,
  pollingIntervalMs: 4000,
  operationalPollingIntervalMs: 30000,
  metricsPollingIntervalMs: 60000,
  highlightDurationMs: 2000,
};
