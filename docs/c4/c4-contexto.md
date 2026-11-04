# C4 — Nível 1: Contexto do Sistema

Visão de quem usa o CashFlow e com quais sistemas ele se relaciona. Fonte formal: [`workspace.dsl`](workspace.dsl).

```mermaid
C4Context
    title CashFlow — Diagrama de Contexto (C1)

    Person(operador, "Operador de Back-office", "Registra créditos/débitos e abre contas")
    Person(analista, "Analista / Gestor", "Consulta saldos e relatórios diários")

    System(cashflow, "CashFlow", "Registra movimentações e fornece a posição consolidada de saldo em qualquer instante")

    System_Ext(canais, "Canais Digitais do Banco", "App / Internet Banking / integrações que originam movimentações")
    System_Ext(contab, "Contabilidade e Relatórios Regulatórios", "Consomem posições consolidadas")
    System_Ext(siem, "SIEM / Observabilidade", "Logs, métricas e traces")

    Rel(operador, cashflow, "Usa o Console Web", "HTTPS")
    Rel(analista, cashflow, "Consulta saldos", "HTTPS")
    Rel(canais, cashflow, "Registra lançamentos", "HTTPS + JWT + Idempotency-Key")
    Rel(contab, cashflow, "Consulta consolidados", "HTTPS + JWT")
    Rel(cashflow, siem, "Exporta telemetria", "OTLP")

    UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

| Elemento | Responsabilidade | Interação |
|---|---|---|
| Operador | Opera contas e lançamentos manuais | Console Web (UI) |
| Analista / Gestor | Acompanha saldos e consolidado diário | Console Web (UI) |
| Canais Digitais | Originam a maior parte das movimentações | API REST via Front Door/Gateway |
| Contabilidade / Regulatório | Consomem posições consolidadas | API REST (somente leitura) |
| SIEM / Observabilidade | Monitoramento, auditoria e alertas | OTLP / Log Analytics |
