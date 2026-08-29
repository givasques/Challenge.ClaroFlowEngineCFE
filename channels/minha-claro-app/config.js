// Configuração do canal App Minha Claro simulado.
// CHANNEL_TOKEN é mockado (ver spec-tecnica §7.2 e §11.1) — não é segurança real, só identifica o canal para o CFE.
//
// apiBaseUrl/whatsappSimUrl usam window.location.origin para funcionar tanto local (modo full) quanto em produção
// (Render), sem precisar de variáveis de build por ambiente — o canal é sempre servido pela própria API,
// então a origem da página já é o host correto.
// Exceção: modo dev isolado (dotnet run na 5104 + http-server por canal nas portas 5171/5173/5175), onde a
// página não é servida pela API — nesse caso apontamos explicitamente para localhost:5104.
const IS_ISOLATED_DEV = ['5171', '5173', '5175'].includes(window.location.port);

const CFE_CONFIG = {
  apiBaseUrl: IS_ISOLATED_DEV ? 'http://localhost:5104' : window.location.origin,
  channelToken: 'fake-app-token',
  whatsappSimUrl: IS_ISOLATED_DEV ? 'http://localhost:5171' : `${window.location.origin}/channels/whatsapp-sim`,
};
