---
status: accepted
date: 2026-10-29
---

# ADR-0009 — Azure Container Apps como plataforma de execução

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
Cinco containers stateless (+ broker) com necessidade de autoscaling (HTTP e profundidade de fila), revisões para deploy seguro, rede privada e baixo esforço operacional. Ecossistema Microsoft (.NET, Azure DevOps) é o padrão da vaga/cliente.

## Decisão
Executar em **Azure Container Apps (perfil Consumption)** dentro de VNet própria, com:
- Identidade gerenciada por serviço (AcrPull + Key Vault Secrets User — menor privilégio).
- Segredos referenciados do **Key Vault** (sem valores no app).
- Ingress externo apenas para Gateway e Web, ambos protegidos pelo Front Door (validação `X-Azure-FDID`); demais apps com ingress interno.
- Escala: HTTP concurrency para APIs; **KEDA rabbitmq** (profundidade de fila) para o Consolidado; mínimo de 2 réplicas em produção (zona redundante).
- Probes de liveness/readiness nos endpoints `/health/live` e `/health/ready`.

IaC em [`infra/terraform`](../../infra/terraform) (módulos reutilizáveis + ambientes `dev`/`prod`).

## Alternativas consideradas
| Opção | Por que não agora |
|---|---|
| **AKS** | Controle total e operador RabbitMQ, mas exige equipe de plataforma (upgrades, nós, políticas). Recomendado se a organização já opera AKS. |
| **App Service** | Simples, porém sem KEDA por fila e escala menos granular por serviço. |
| **VMs** | Maior custo operacional e menor elasticidade. |

## Consequências
- ➕ Custo proporcional ao uso (dev escala a zero), operação mínima, autoscaling nativo por fila.
- ➖ Menos controle de rede/host que AKS; RabbitMQ em cluster é mais simples fora do ACA (ver ADR-0003).
