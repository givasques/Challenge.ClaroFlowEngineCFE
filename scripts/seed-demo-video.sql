-- ===========================================================================
-- Seed de demonstração para gravação do vídeo (specs/seed-demo-video.md)
--
-- Só INSERT (e DELETE das linhas que este próprio script criou, pelos UUIDs
-- fixos abaixo, para poder repetir sem duplicar). Não cria, recria nem altera
-- nenhum cliente, usuário do painel ou jornada que já existia antes dele.
--
-- Usa só tabelas e colunas que existem tanto na v-sprint3 (../cfe-antes)
-- quanto na feat/sprint4 (customers, identity_links, plans, customer_plans,
-- journey_contexts, journey_transitions) — conferido com \d nas duas antes
-- de escrever este script; os dois esquemas são idênticos nessas tabelas.
--
-- Pode ser executado de novo antes de cada gravação: as jornadas da fila
-- (seção 2) são apagadas e recriadas com os minutos de inatividade relativos
-- ao momento da execução (NOW() - INTERVAL), porque jornada aberta "envelhece"
-- em tempo real.
-- ===========================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 0) Limpeza da execução anterior deste seed (idempotência por UUID fixo)
-- ---------------------------------------------------------------------------
DELETE FROM journey_transitions WHERE journey_context_id = ANY(ARRAY[
  'c1000000-0000-4000-8000-000000000001', 'c1000000-0000-4000-8000-000000000002',
  'c1000000-0000-4000-8000-000000000003', 'c1000000-0000-4000-8000-000000000004',
  'c1000000-0000-4000-8000-000000000005',
  'c2000000-0000-4000-8000-000000000001', 'c2000000-0000-4000-8000-000000000002',
  'c2000000-0000-4000-8000-000000000003', 'c2000000-0000-4000-8000-000000000004',
  'c2000000-0000-4000-8000-000000000005', 'c2000000-0000-4000-8000-000000000006'
]::uuid[]);
DELETE FROM journey_contexts WHERE id = ANY(ARRAY[
  'c1000000-0000-4000-8000-000000000001', 'c1000000-0000-4000-8000-000000000002',
  'c1000000-0000-4000-8000-000000000003', 'c1000000-0000-4000-8000-000000000004',
  'c1000000-0000-4000-8000-000000000005',
  'c2000000-0000-4000-8000-000000000001', 'c2000000-0000-4000-8000-000000000002',
  'c2000000-0000-4000-8000-000000000003', 'c2000000-0000-4000-8000-000000000004',
  'c2000000-0000-4000-8000-000000000005', 'c2000000-0000-4000-8000-000000000006'
]::uuid[]);
DELETE FROM identity_links WHERE customer_id = ANY(ARRAY[
  'd1000000-0000-4000-8000-000000000001', 'd1000000-0000-4000-8000-000000000002',
  'd1000000-0000-4000-8000-000000000003', 'd1000000-0000-4000-8000-000000000004'
]::uuid[]);
DELETE FROM customers WHERE id = ANY(ARRAY[
  'd1000000-0000-4000-8000-000000000001', 'd1000000-0000-4000-8000-000000000002',
  'd1000000-0000-4000-8000-000000000003', 'd1000000-0000-4000-8000-000000000004'
]::uuid[]);

-- ---------------------------------------------------------------------------
-- 0.1) Personas novas pra fila (só as que não existem nas duas versões —
--      Carlos Mendes e Mariana Souza já existem nas duas e são reaproveitados
--      direto na seção 2, sem recriar).
-- ---------------------------------------------------------------------------
INSERT INTO customers (id, cpf, full_name, created_at, billing_due_day, segment) VALUES
  ('d1000000-0000-4000-8000-000000000001', '52998224725', 'João Silva', NOW() - INTERVAL '400 days', 10, 'Pessoa Física'),
  ('d1000000-0000-4000-8000-000000000002', '39053344705', 'Fernanda Lima', NOW() - INTERVAL '250 days', 18, 'Pessoa Física'),
  ('d1000000-0000-4000-8000-000000000003', '25806468844', 'Marcos Pereira', NOW() - INTERVAL '500 days', 5, 'Premium'),
  ('d1000000-0000-4000-8000-000000000004', '15350946056', 'Patrícia Souza', NOW() - INTERVAL '200 days', 25, 'Pessoa Física');
INSERT INTO identity_links (channel, customer_id, identifier) VALUES
  ('cpf', 'd1000000-0000-4000-8000-000000000001', '52998224725'),
  ('cpf', 'd1000000-0000-4000-8000-000000000002', '39053344705'),
  ('cpf', 'd1000000-0000-4000-8000-000000000003', '25806468844'),
  ('cpf', 'd1000000-0000-4000-8000-000000000004', '15350946056');

-- ---------------------------------------------------------------------------
-- 1) Ana Silva (CPF 11144477735) — histórico completo pra 5 jornadas.
--
-- Na feat/sprint4, a Ana já nasce com 2 trocas de plano concluídas e 1
-- abandonada (seed padrão de contas do App). Na v-sprint3 (../cfe-antes),
-- ela nasce sem nenhuma jornada. Por isso as 3 jornadas de troca de plano
-- abaixo só são inseridas quando ela ainda não tem NENHUMA jornada desse
-- intent — nas duas versões o resultado final é o mesmo: 3 trocas de plano
-- (2 concluídas + 1 abandonada) + as 2 contestações inseridas sempre
-- (concluída e escalada) = 5 jornadas no total.
-- ---------------------------------------------------------------------------

-- Ana · change_plan · concluded · há 45 dias (só se ela ainda não tiver troca de plano)
INSERT INTO journey_contexts (id, customer_id, origin_channel, intent, current_step, payload, status, created_at, updated_at, closed_at, escalated_at)
SELECT 'c1000000-0000-4000-8000-000000000003', id, 'whatsapp', 'change_plan', 'plan_selected',
  '{"selected_plan_code": "claro_30gb", "_demo_seed": true}'::jsonb, 'concluded', NOW() - INTERVAL '45 days', (NOW() - INTERVAL '45 days' + INTERVAL '3 minutes'), (NOW() - INTERVAL '45 days' + INTERVAL '3 minutes'), NULL
FROM customers WHERE cpf = '11144477735'
  AND NOT EXISTS (SELECT 1 FROM journey_contexts jc JOIN customers cc ON cc.id = jc.customer_id WHERE cc.cpf = '11144477735' AND jc.intent = 'change_plan');
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at)
SELECT 'c1000000-0000-4000-8000-000000000003', 'whatsapp', 'journey_started', 'Jornada iniciada.', '{"intent": "change_plan", "initial_step": "plan_selection"}'::jsonb, (NOW() - INTERVAL '45 days' + INTERVAL '0 seconds')
WHERE EXISTS (SELECT 1 FROM journey_contexts WHERE id = 'c1000000-0000-4000-8000-000000000003');
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at)
SELECT 'c1000000-0000-4000-8000-000000000003', 'whatsapp', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "plan_selection", "current_step": "plan_selected", "updated_keys": ["selected_plan_code"]}'::jsonb, (NOW() - INTERVAL '45 days' + INTERVAL '30 seconds')
WHERE EXISTS (SELECT 1 FROM journey_contexts WHERE id = 'c1000000-0000-4000-8000-000000000003');
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at)
SELECT 'c1000000-0000-4000-8000-000000000003', 'whatsapp', 'journey_closed', 'Jornada encerrada como ''concluded''.', '{"outcome": "concluded", "reason": null}'::jsonb, (NOW() - INTERVAL '45 days' + INTERVAL '180 seconds')
WHERE EXISTS (SELECT 1 FROM journey_contexts WHERE id = 'c1000000-0000-4000-8000-000000000003');

-- Ana · change_plan · concluded · há 15 dias (mesma condição)
INSERT INTO journey_contexts (id, customer_id, origin_channel, intent, current_step, payload, status, created_at, updated_at, closed_at, escalated_at)
SELECT 'c1000000-0000-4000-8000-000000000004', id, 'app', 'change_plan', 'plan_selected',
  '{"selected_plan_code": "claro_60gb", "_demo_seed": true}'::jsonb, 'concluded', NOW() - INTERVAL '15 days', (NOW() - INTERVAL '15 days' + INTERVAL '3 minutes'), (NOW() - INTERVAL '15 days' + INTERVAL '3 minutes'), NULL
FROM customers WHERE cpf = '11144477735'
  AND NOT EXISTS (SELECT 1 FROM journey_contexts jc JOIN customers cc ON cc.id = jc.customer_id WHERE cc.cpf = '11144477735' AND jc.intent = 'change_plan' AND jc.id <> 'c1000000-0000-4000-8000-000000000003');
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at)
SELECT 'c1000000-0000-4000-8000-000000000004', 'app', 'journey_started', 'Jornada iniciada.', '{"intent": "change_plan", "initial_step": "plan_selection"}'::jsonb, (NOW() - INTERVAL '15 days' + INTERVAL '0 seconds')
WHERE EXISTS (SELECT 1 FROM journey_contexts WHERE id = 'c1000000-0000-4000-8000-000000000004');
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at)
SELECT 'c1000000-0000-4000-8000-000000000004', 'app', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "plan_selection", "current_step": "plan_selected", "updated_keys": ["selected_plan_code"]}'::jsonb, (NOW() - INTERVAL '15 days' + INTERVAL '30 seconds')
WHERE EXISTS (SELECT 1 FROM journey_contexts WHERE id = 'c1000000-0000-4000-8000-000000000004');
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at)
SELECT 'c1000000-0000-4000-8000-000000000004', 'app', 'journey_closed', 'Jornada encerrada como ''concluded''.', '{"outcome": "concluded", "reason": null}'::jsonb, (NOW() - INTERVAL '15 days' + INTERVAL '180 seconds')
WHERE EXISTS (SELECT 1 FROM journey_contexts WHERE id = 'c1000000-0000-4000-8000-000000000004');

-- Ana · change_plan · abandoned · há 5 dias (mesma condição)
INSERT INTO journey_contexts (id, customer_id, origin_channel, intent, current_step, payload, status, created_at, updated_at, closed_at, escalated_at)
SELECT 'c1000000-0000-4000-8000-000000000005', id, 'whatsapp', 'change_plan', 'plan_selected',
  '{"selected_plan_code": "claro_30gb", "_demo_seed": true}'::jsonb, 'abandoned', NOW() - INTERVAL '5 days', (NOW() - INTERVAL '5 days' + INTERVAL '2.5 minutes'), (NOW() - INTERVAL '5 days' + INTERVAL '2.5 minutes'), NULL
FROM customers WHERE cpf = '11144477735'
  AND NOT EXISTS (SELECT 1 FROM journey_contexts jc JOIN customers cc ON cc.id = jc.customer_id WHERE cc.cpf = '11144477735' AND jc.intent = 'change_plan' AND jc.id NOT IN ('c1000000-0000-4000-8000-000000000003', 'c1000000-0000-4000-8000-000000000004'));
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at)
SELECT 'c1000000-0000-4000-8000-000000000005', 'whatsapp', 'journey_started', 'Jornada iniciada.', '{"intent": "change_plan", "initial_step": "plan_selection"}'::jsonb, (NOW() - INTERVAL '5 days' + INTERVAL '0 seconds')
WHERE EXISTS (SELECT 1 FROM journey_contexts WHERE id = 'c1000000-0000-4000-8000-000000000005');
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at)
SELECT 'c1000000-0000-4000-8000-000000000005', 'whatsapp', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "plan_selection", "current_step": "plan_selected", "updated_keys": ["selected_plan_code"]}'::jsonb, (NOW() - INTERVAL '5 days' + INTERVAL '25 seconds')
WHERE EXISTS (SELECT 1 FROM journey_contexts WHERE id = 'c1000000-0000-4000-8000-000000000005');
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at)
SELECT 'c1000000-0000-4000-8000-000000000005', 'whatsapp', 'journey_closed', 'Jornada encerrada como ''abandoned''.', '{"outcome": "abandoned", "reason": "customer_gave_up"}'::jsonb, (NOW() - INTERVAL '5 days' + INTERVAL '150 seconds')
WHERE EXISTS (SELECT 1 FROM journey_contexts WHERE id = 'c1000000-0000-4000-8000-000000000005');

-- Ana · dispute_charge · concluded · há 22 dias
INSERT INTO journey_contexts (id, customer_id, origin_channel, intent, current_step, payload, status, created_at, updated_at, closed_at, escalated_at)
VALUES ('c1000000-0000-4000-8000-000000000001', (SELECT id FROM customers WHERE cpf = '11144477735'), 'app', 'dispute_charge', 'dispute_formalized',
  '{"invoice_id": "2026-09", "dispute_reason": "service_not_contracted", "customer_description": "Cobrança de serviço adicional que não contratei", "protocol_number": "CFE-611829", "_demo_seed": true}'::jsonb,
  'concluded', NOW() - INTERVAL '22 days', (NOW() - INTERVAL '22 days' + INTERVAL '4 minutes'), (NOW() - INTERVAL '22 days' + INTERVAL '4 minutes'), NULL);
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at) VALUES
  ('c1000000-0000-4000-8000-000000000001', 'app', 'journey_started', 'Jornada iniciada.', '{"intent": "dispute_charge", "initial_step": "invoice_selection"}'::jsonb, (NOW() - INTERVAL '22 days' + INTERVAL '0 seconds')),
  ('c1000000-0000-4000-8000-000000000001', 'app', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "invoice_selection", "current_step": "invoice_selected", "updated_keys": ["invoice_id"]}'::jsonb, (NOW() - INTERVAL '22 days' + INTERVAL '30 seconds')),
  ('c1000000-0000-4000-8000-000000000001', 'app', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "invoice_selected", "current_step": "dispute_reason_selected", "updated_keys": ["dispute_reason"]}'::jsonb, (NOW() - INTERVAL '22 days' + INTERVAL '90 seconds')),
  ('c1000000-0000-4000-8000-000000000001', 'app', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "dispute_reason_selected", "current_step": "description_provided", "updated_keys": ["customer_description"]}'::jsonb, (NOW() - INTERVAL '22 days' + INTERVAL '150 seconds')),
  ('c1000000-0000-4000-8000-000000000001', 'app', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "description_provided", "current_step": "dispute_formalized", "updated_keys": ["protocol_number"]}'::jsonb, (NOW() - INTERVAL '22 days' + INTERVAL '210 seconds')),
  ('c1000000-0000-4000-8000-000000000001', 'app', 'journey_closed', 'Jornada encerrada como ''concluded''.', '{"outcome": "concluded", "reason": null}'::jsonb, (NOW() - INTERVAL '22 days' + INTERVAL '240 seconds'));

-- Ana · dispute_charge · escalated · há 8 dias
INSERT INTO journey_contexts (id, customer_id, origin_channel, intent, current_step, payload, status, created_at, updated_at, closed_at, escalated_at)
VALUES ('c1000000-0000-4000-8000-000000000002', (SELECT id FROM customers WHERE cpf = '11144477735'), 'whatsapp', 'dispute_charge', 'description_provided',
  '{"invoice_id": "2026-10", "dispute_reason": "duplicate_charge", "customer_description": "Cobrança em duplicidade na última fatura", "escalation_area": "financial", "escalation_description": "Cliente contesta duplicidade; requer verificação do Financeiro", "_demo_seed": true}'::jsonb,
  'escalated', NOW() - INTERVAL '8 days', (NOW() - INTERVAL '8 days' + INTERVAL '5 minutes'), NULL, (NOW() - INTERVAL '8 days' + INTERVAL '5 minutes'));
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at) VALUES
  ('c1000000-0000-4000-8000-000000000002', 'whatsapp', 'journey_started', 'Jornada iniciada.', '{"intent": "dispute_charge", "initial_step": "invoice_selection"}'::jsonb, (NOW() - INTERVAL '8 days' + INTERVAL '0 seconds')),
  ('c1000000-0000-4000-8000-000000000002', 'whatsapp', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "invoice_selection", "current_step": "invoice_selected", "updated_keys": ["invoice_id"]}'::jsonb, (NOW() - INTERVAL '8 days' + INTERVAL '30 seconds')),
  ('c1000000-0000-4000-8000-000000000002', 'whatsapp', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "invoice_selected", "current_step": "dispute_reason_selected", "updated_keys": ["dispute_reason"]}'::jsonb, (NOW() - INTERVAL '8 days' + INTERVAL '90 seconds')),
  ('c1000000-0000-4000-8000-000000000002', 'whatsapp', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "dispute_reason_selected", "current_step": "description_provided", "updated_keys": ["customer_description"]}'::jsonb, (NOW() - INTERVAL '8 days' + INTERVAL '150 seconds')),
  ('c1000000-0000-4000-8000-000000000002', 'panel', 'journey_escalated', 'Jornada escalada para Financeiro / Cobrança.', '{"escalation_area": "financial", "description": "Cliente contesta duplicidade; requer verificação do Financeiro"}'::jsonb, (NOW() - INTERVAL '8 days' + INTERVAL '300 seconds'));

-- ---------------------------------------------------------------------------
-- 2) Fila de Jornadas Ativas — 6 jornadas abertas, clientes/intenções/canais
--    diferentes, inatividade escalonada (2, 7, 12, 20, 35 e 50 minutos) pra
--    a coluna de tempo mostrar as faixas (normal/atenção/crítica).
--    Reaproveita Carlos e Mariana (já existem nas duas versões) + as 4
--    personas novas criadas na seção 0.1 (também nas duas versões).
-- ---------------------------------------------------------------------------

-- Carlos Mendes · change_plan · whatsapp · aberta há 2 min
INSERT INTO journey_contexts (id, customer_id, origin_channel, intent, current_step, payload, status, created_at, updated_at, closed_at, escalated_at)
VALUES ('c2000000-0000-4000-8000-000000000001', (SELECT id FROM customers WHERE cpf = '22255588846'), 'whatsapp', 'change_plan', 'plan_selection',
  '{"_demo_seed": true}'::jsonb, 'open', NOW() - INTERVAL '2 minutes', NOW() - INTERVAL '2 minutes', NULL, NULL);
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at) VALUES
  ('c2000000-0000-4000-8000-000000000001', 'whatsapp', 'journey_started', 'Jornada iniciada.', '{"intent": "change_plan", "initial_step": "plan_selection"}'::jsonb, NOW() - INTERVAL '2 minutes');

-- Mariana Souza · dispute_charge · app · aberta há 7 min
INSERT INTO journey_contexts (id, customer_id, origin_channel, intent, current_step, payload, status, created_at, updated_at, closed_at, escalated_at)
VALUES ('c2000000-0000-4000-8000-000000000002', (SELECT id FROM customers WHERE cpf = '33366699957'), 'app', 'dispute_charge', 'invoice_selected',
  '{"invoice_id": "2026-10", "_demo_seed": true}'::jsonb, 'open', NOW() - INTERVAL '7 minutes', NOW() - INTERVAL '7 minutes', NULL, NULL);
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at) VALUES
  ('c2000000-0000-4000-8000-000000000002', 'app', 'journey_started', 'Jornada iniciada.', '{"intent": "dispute_charge", "initial_step": "invoice_selection"}'::jsonb, NOW() - INTERVAL '7 minutes'),
  ('c2000000-0000-4000-8000-000000000002', 'app', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "invoice_selection", "current_step": "invoice_selected", "updated_keys": ["invoice_id"]}'::jsonb, NOW() - INTERVAL '6 minutes 30 seconds');

-- João Silva · change_plan · app · aberta há 12 min
INSERT INTO journey_contexts (id, customer_id, origin_channel, intent, current_step, payload, status, created_at, updated_at, closed_at, escalated_at)
VALUES ('c2000000-0000-4000-8000-000000000003', 'd1000000-0000-4000-8000-000000000001', 'app', 'change_plan', 'plan_selection',
  '{"_demo_seed": true}'::jsonb, 'open', NOW() - INTERVAL '12 minutes', NOW() - INTERVAL '12 minutes', NULL, NULL);
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at) VALUES
  ('c2000000-0000-4000-8000-000000000003', 'app', 'journey_started', 'Jornada iniciada.', '{"intent": "change_plan", "initial_step": "plan_selection"}'::jsonb, NOW() - INTERVAL '12 minutes');

-- Fernanda Lima · dispute_charge · whatsapp · aberta há 20 min
INSERT INTO journey_contexts (id, customer_id, origin_channel, intent, current_step, payload, status, created_at, updated_at, closed_at, escalated_at)
VALUES ('c2000000-0000-4000-8000-000000000004', 'd1000000-0000-4000-8000-000000000002', 'whatsapp', 'dispute_charge', 'dispute_reason_selected',
  '{"invoice_id": "2026-10", "dispute_reason": "higher_than_expected", "_demo_seed": true}'::jsonb, 'open', NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '20 minutes', NULL, NULL);
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at) VALUES
  ('c2000000-0000-4000-8000-000000000004', 'whatsapp', 'journey_started', 'Jornada iniciada.', '{"intent": "dispute_charge", "initial_step": "invoice_selection"}'::jsonb, NOW() - INTERVAL '20 minutes'),
  ('c2000000-0000-4000-8000-000000000004', 'whatsapp', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "invoice_selection", "current_step": "invoice_selected", "updated_keys": ["invoice_id"]}'::jsonb, NOW() - INTERVAL '19 minutes 30 seconds'),
  ('c2000000-0000-4000-8000-000000000004', 'whatsapp', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "invoice_selected", "current_step": "dispute_reason_selected", "updated_keys": ["dispute_reason"]}'::jsonb, NOW() - INTERVAL '18 minutes');

-- Marcos Pereira · change_plan · whatsapp · aberta há 35 min
INSERT INTO journey_contexts (id, customer_id, origin_channel, intent, current_step, payload, status, created_at, updated_at, closed_at, escalated_at)
VALUES ('c2000000-0000-4000-8000-000000000005', 'd1000000-0000-4000-8000-000000000003', 'whatsapp', 'change_plan', 'plan_selected',
  '{"selected_plan_code": "claro_60gb", "_demo_seed": true}'::jsonb, 'open', NOW() - INTERVAL '35 minutes', NOW() - INTERVAL '35 minutes', NULL, NULL);
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at) VALUES
  ('c2000000-0000-4000-8000-000000000005', 'whatsapp', 'journey_started', 'Jornada iniciada.', '{"intent": "change_plan", "initial_step": "plan_selection"}'::jsonb, NOW() - INTERVAL '35 minutes'),
  ('c2000000-0000-4000-8000-000000000005', 'whatsapp', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "plan_selection", "current_step": "plan_selected", "updated_keys": ["selected_plan_code"]}'::jsonb, NOW() - INTERVAL '34 minutes 30 seconds');

-- Patrícia Souza · dispute_charge · call · aberta há 50 min
INSERT INTO journey_contexts (id, customer_id, origin_channel, intent, current_step, payload, status, created_at, updated_at, closed_at, escalated_at)
VALUES ('c2000000-0000-4000-8000-000000000006', 'd1000000-0000-4000-8000-000000000004', 'call', 'dispute_charge', 'description_provided',
  '{"invoice_id": "2026-10", "dispute_reason": "duplicate_charge", "customer_description": "Cobrança duplicada no mesmo mês", "_demo_seed": true}'::jsonb, 'open', NOW() - INTERVAL '50 minutes', NOW() - INTERVAL '50 minutes', NULL, NULL);
INSERT INTO journey_transitions (journey_context_id, channel, event_type, description, metadata, occurred_at) VALUES
  ('c2000000-0000-4000-8000-000000000006', 'call', 'journey_started', 'Jornada iniciada.', '{"intent": "dispute_charge", "initial_step": "invoice_selection"}'::jsonb, NOW() - INTERVAL '50 minutes'),
  ('c2000000-0000-4000-8000-000000000006', 'call', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "invoice_selection", "current_step": "invoice_selected", "updated_keys": ["invoice_id"]}'::jsonb, NOW() - INTERVAL '49 minutes 30 seconds'),
  ('c2000000-0000-4000-8000-000000000006', 'call', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "invoice_selected", "current_step": "dispute_reason_selected", "updated_keys": ["dispute_reason"]}'::jsonb, NOW() - INTERVAL '48 minutes'),
  ('c2000000-0000-4000-8000-000000000006', 'call', 'step_updated', 'Etapa e/ou dados da jornada atualizados.', '{"previous_step": "dispute_reason_selected", "current_step": "description_provided", "updated_keys": ["customer_description"]}'::jsonb, NOW() - INTERVAL '46 minutes');

COMMIT;
