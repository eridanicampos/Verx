---
status: accepted
date: 2026-10-29
---

# ADR-0004 — Contratos de eventos sem projeto compartilhado

- **Status:** Aceito (substitui a abordagem com `CashFlow.Shared`)
- **Data:** 2026-10-29

## Contexto
Uma versão anterior da solução usava um projeto `CashFlow.Shared` referenciado por Lançamentos e Consolidado contendo DTOs de eventos e utilitários. Isso cria **acoplamento em tempo de compilação**: qualquer mudança em `Shared` exige recompilar e reimplantar ambos os serviços juntos — característica de **monólito distribuído**.

## Decisão
- **Não existe projeto compartilhado entre serviços.** Cada serviço tem seus próprios tipos.
- O **produtor** é dono do contrato (`CashFlow.Transactions.Application/IntegrationEvents/*V1`), publicado como documentação em [`docs/contratos/asyncapi.yaml`](../contratos/asyncapi.yaml) com exemplos JSON em `docs/contratos/exemplos`.
- O **consumidor** declara seu **modelo de leitura próprio** (`CashFlow.Consolidation.Application/Messages`) seguindo *tolerant reader* (ignora campos desconhecidos).
- **Testes de contrato** dos dois lados validam contra os mesmos exemplos JSON (acoplamento por arquivo/esquema, não por assembly):
  - Produtor: `ProducerContractTests` — o JSON serializado tem exatamente os campos do exemplo.
  - Consumidor: `ConsumerContractTests` — o exemplo é desserializado e aceito pelas regras de validação.
- Versionamento no nome do evento (`.v1`). Mudança incompatível = novo evento `.v2` publicado em paralelo até migração do consumidor.
- Pequenas duplicações (ex.: `Result`, `SnakeCaseNamingExtensions`, conexão RabbitMQ) são **aceitas deliberadamente**: o custo de duplicar ~100 linhas é menor que o de acoplar ciclos de release.

## Alternativas consideradas
- **Pacote NuGet de contratos versionado:** melhor que referência de projeto, mas ainda propaga dependência binária e tende a virar "Shared" de novo. Pode ser reavaliado se surgirem muitos consumidores (gerar a partir do AsyncAPI).
- **Schema Registry (Avro/Protobuf):** robusto, porém infraestrutura extra desproporcional para dois eventos.

## Consequências
- ➕ Deploy independente real; times podem evoluir em ritmos diferentes.
- ➕ Quebra de contrato é detectada no CI antes do deploy.
- ➖ Duplicação controlada de código utilitário.
