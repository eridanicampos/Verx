---
status: accepted
date: 2026-10-29
---

# ADR-0011 — PostgreSQL como banco relacional

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
Dados financeiros exigem ACID, precisão decimal, constraints de unicidade (idempotência) e bom suporte a concorrência. Precisamos de recursos específicos: `FOR UPDATE SKIP LOCKED` (outbox com várias réplicas), `INSERT … ON CONFLICT` (projeção idempotente), índices parciais.

## Decisão
- **PostgreSQL 16+** (local 17, nuvem Azure Database for PostgreSQL Flexible Server 16), via EF Core 10 + Npgsql (licença PostgreSQL).
- `numeric(18,2)` para valores; `timestamptz` em UTC; IDs UUIDv7.
- **Database-per-service** com usuário próprio por serviço (menor privilégio). Migrations versionadas (EF Core); em produção executadas pelo pipeline com usuário DDL, não pela aplicação.
- Produção: HA zona redundante, PITR de 35 dias, backup geo-redundante, TLS obrigatório, acesso apenas pela VNet.

## Alternativas consideradas
- **SQL Server / Azure SQL:** excelente com .NET; licenciamento e custo maiores; `READPAST`/`MERGE` cobrem os mesmos padrões. Viável se for padrão corporativo.
- **NoSQL (Cosmos DB):** bom para leitura em escala, mas transações multi-documento e constraints de unicidade são mais limitadas para o lado transacional.

## Consequências
- ➕ Open source, portável, recursos de concorrência alinhados aos padrões adotados.
- ➖ SQL específico de PostgreSQL no outbox/projeção (isolado na Infrastructure).
