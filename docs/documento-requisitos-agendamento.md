# Documento de Requisitos — Jornada de Agendamento

## 1. Objetivo
Definir os requisitos funcionais e regras de negócio para a jornada de agendamento de consultas, cobrindo busca de profissional, disponibilidade, reserva, confirmação, cancelamento e reagendamento.

## 2. Escopo
Este documento contempla:
- Fluxos principais do paciente e da recepção.
- Regras críticas de agendamento e conflito.
- Perfis de usuário e permissões básicas.
- Mensagens de erro e estados de UI por etapa.
- Plano de validação com stakeholders (clínica, operação e jurídico/LGPD).

Fora de escopo (neste ciclo):
- Integrações com convênios/operadoras.
- Pagamento online.
- Gestão clínica (prontuário/evolução).

---

## 3. Papéis de Usuário

### 3.1 Paciente autenticado
**Descrição:** usuário final que agenda a própria consulta.

**Permissões:**
- Buscar profissional por especialidade e unidade.
- Visualizar disponibilidade por data/período.
- Reservar horário disponível.
- Confirmar agendamento.
- Cancelar agendamento dentro das regras.
- Reagendar agendamento existente, respeitando políticas.

### 3.2 Recepcionista (se aplicável)
**Descrição:** colaborador da clínica que realiza agendamentos em nome do paciente.

**Permissões adicionais em relação ao paciente:**
- Agendar para pacientes diferentes (com vínculo por CPF/ID).
- Visualizar agenda ampliada da unidade.
- Reagendar/cancelar com justificativa operacional.
- Aplicar exceções permitidas por política interna (com auditoria).

**Observação:** toda ação de recepcionista deve registrar `usuario_executor`, `paciente_alvo`, data/hora e motivo (quando aplicável).

---

## 4. Fluxos Principais

### 4.1 Buscar profissional por especialidade/unidade
**Pré-condições:**
- Usuário autenticado.
- Especialidades e unidades cadastradas.

**Fluxo principal:**
1. Usuário acessa tela de busca.
2. Seleciona especialidade.
3. Seleciona unidade (opcional, dependendo da regra de negócio da rede).
4. Sistema lista profissionais ativos compatíveis.
5. Usuário seleciona profissional para seguir para disponibilidade.

**Pós-condição:** profissional selecionado em contexto para consulta de agenda.

**Regras complementares:**
- Exibir apenas profissionais ativos e com agenda publicada.
- Caso unidade não seja selecionada, permitir busca multiunidade com indicação da unidade de cada slot.

### 4.2 Listar disponibilidade por data/período
**Pré-condições:**
- Profissional selecionado.
- Agenda do profissional publicada para a unidade.

**Fluxo principal:**
1. Usuário escolhe data (ou intervalo).
2. Usuário define período (manhã/tarde/noite ou horário específico).
3. Sistema retorna slots disponíveis.
4. Usuário escolhe um slot.

**Pós-condição:** slot selecionado para reserva.

**Regras complementares:**
- Mostrar apenas horários dentro da janela mínima/máxima permitida.
- Não exibir slots bloqueados, já reservados ou indisponíveis por regras internas.

### 4.3 Reservar horário
**Pré-condições:**
- Slot válido e ainda disponível no momento da ação.
- Paciente elegível para agendar (cadastro e políticas em dia).

**Fluxo principal:**
1. Usuário confirma intenção de reservar o slot.
2. Sistema valida conflitos e regras de negócio.
3. Sistema cria reserva com status `PENDENTE_CONFIRMACAO` (ou `RESERVADO`).
4. Sistema retorna resumo da reserva e prazo para confirmação (se aplicável).

**Pós-condição:** reserva registrada com identificador único.

**Regras complementares:**
- Operação deve ser idempotente para evitar dupla criação em caso de retry.
- Bloqueio concorrente: apenas uma reserva pode vencer para o mesmo slot.

### 4.4 Confirmar, cancelar e reagendar

#### 4.4.1 Confirmar
**Fluxo principal:**
1. Usuário acessa detalhes do agendamento reservado.
2. Usuário confirma o agendamento.
3. Sistema atualiza status para `CONFIRMADO`.
4. Sistema emite confirmação (tela + notificação).

#### 4.4.2 Cancelar
**Fluxo principal:**
1. Usuário acessa agendamento existente.
2. Usuário seleciona cancelar e informa motivo (opcional para paciente, obrigatório para recepção).
3. Sistema valida política de cancelamento (prazo mínimo, penalidades).
4. Sistema atualiza status para `CANCELADO` e libera slot conforme regra.

#### 4.4.3 Reagendar
**Fluxo principal:**
1. Usuário seleciona reagendar em um agendamento elegível.
2. Sistema direciona para busca de nova disponibilidade.
3. Usuário escolhe novo slot.
4. Sistema valida conflitos e políticas.
5. Sistema confirma novo agendamento e encerra anterior como `REAGENDADO`.

**Pós-condição:** apenas um agendamento ativo por jornada de atendimento.

---

## 5. Regras de Negócio Críticas

### 5.1 Janela mínima/máxima para agendamento
- **Janela mínima:** não permitir agendar com antecedência inferior a **X horas** do horário da consulta.
- **Janela máxima:** não permitir agendar além de **Y dias** no futuro.
- Valores de X e Y devem ser configuráveis por especialidade/unidade.

### 5.2 Regras para conflito de horário
- Um mesmo slot só pode ter um agendamento ativo (`RESERVADO`/`CONFIRMADO`).
- Paciente não pode ter dois agendamentos sobrepostos.
- Recepcionista deve receber alerta de conflito antes de concluir operação.
- Em concorrência, o sistema deve garantir consistência transacional e retornar erro de conflito para a tentativa perdedora.

### 5.3 Políticas de cancelamento e no-show
- Cancelamento sem penalidade até **N horas** antes da consulta.
- Cancelamento fora do prazo pode registrar ocorrência para política interna.
- No-show (falta sem comparecimento) deve:
  - marcar status específico (`NO_SHOW`);
  - permitir rastreabilidade histórica;
  - habilitar bloqueios/restrições futuras conforme política da clínica.

**Observação legal/LGPD:** motivos sensíveis não devem ser expostos além do necessário e devem seguir base legal e política de retenção.

---

## 6. Mensagens de Erro e Estados de UI por Etapa

### 6.1 Buscar profissional
**Estados de UI:**
- `loading`: carregando filtros/listagem.
- `empty`: nenhum profissional encontrado.
- `error`: falha ao consultar dados.

**Mensagens sugeridas:**
- `Nenhum profissional encontrado para os filtros selecionados.`
- `Não foi possível carregar os profissionais. Tente novamente.`

### 6.2 Listar disponibilidade
**Estados de UI:**
- `loading`: buscando horários.
- `empty`: sem horários para data/período.
- `error`: indisponibilidade temporária.

**Mensagens sugeridas:**
- `Não há horários disponíveis para esta data/período.`
- `Este horário acabou de ser reservado. Escolha outro horário.`
- `Falha ao carregar disponibilidade. Atualize a página e tente novamente.`

### 6.3 Reservar horário
**Estados de UI:**
- `submitting`: processando reserva.
- `success`: reserva criada.
- `conflict`: horário indisponível por concorrência.
- `validation_error`: regra de negócio violada.

**Mensagens sugeridas:**
- `Sua reserva foi criada com sucesso.`
- `Não foi possível reservar: o horário não está mais disponível.`
- `Você já possui agendamento em horário conflitante.`
- `Não é possível agendar fora da janela permitida.`

### 6.4 Confirmar agendamento
**Estados de UI:**
- `submitting`: confirmando.
- `success`: confirmado.
- `error`: falha de confirmação.

**Mensagens sugeridas:**
- `Agendamento confirmado com sucesso.`
- `Esta reserva expirou e não pode mais ser confirmada.`
- `Não foi possível confirmar agora. Tente novamente em instantes.`

### 6.5 Cancelar agendamento
**Estados de UI:**
- `submitting`: cancelando.
- `success`: cancelado.
- `policy_block`: bloqueado por política.

**Mensagens sugeridas:**
- `Agendamento cancelado com sucesso.`
- `Cancelamento fora do prazo permitido.`
- `Não foi possível cancelar este agendamento.`

### 6.6 Reagendar agendamento
**Estados de UI:**
- `selecting_new_slot`: escolha de novo horário.
- `submitting`: processando reagendamento.
- `success`: reagendado.
- `conflict/error`: falha por concorrência/regra.

**Mensagens sugeridas:**
- `Agendamento reagendado com sucesso.`
- `O novo horário selecionado não está mais disponível.`
- `Não foi possível reagendar por conflito de agenda.`

---

## 7. Requisitos de Auditoria e Conformidade (LGPD)
- Registrar trilha de auditoria para criar/confirmar/cancelar/reagendar.
- Minimizar dados pessoais exibidos em telas operacionais.
- Permitir anonimização/eliminação conforme política de retenção.
- Exigir consentimento e base legal para comunicações (SMS, e-mail, WhatsApp).

---

## 8. Validação com Stakeholders

### 8.1 Stakeholders e responsabilidades
- **Clínica (assistencial):** valida aderência ao fluxo de atendimento.
- **Operação (recepção/central):** valida usabilidade, exceções e SLAs.
- **Jurídico/LGPD:** valida base legal, privacidade e retenção de dados.

### 8.2 Roteiro de validação
1. Revisão assíncrona do documento por cada área.
2. Workshop conjunto para resolução de conflitos de regra.
3. Formalização dos parâmetros de política (X, Y, N e exceções).
4. Aprovação final com registro de versão.

### 8.3 Critérios de aceite
- Fluxos principais aprovados sem pendências críticas.
- Regras de janela, conflito e cancelamento/no-show parametrizadas.
- Mensagens de erro/UI aprovadas por operação e produto.
- Parecer jurídico/LGPD favorável às práticas de tratamento de dados.

### 8.4 Status de validação
- Clínica: **Pendente**
- Operação: **Pendente**
- Jurídico/LGPD: **Pendente**

---

## 9. Pendências para próxima iteração
- Definir valores iniciais de X, Y e N por especialidade/unidade.
- Especificar matriz de permissões detalhada para recepcionista.
- Validar catálogo final de mensagens com UX Writing.
