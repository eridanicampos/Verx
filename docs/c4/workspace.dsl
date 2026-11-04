/*
 * Modelo C4 formal do CashFlow (Structurizr DSL).
 * Visualizar: docker compose -f docs/c4/docker-compose.yml up  ->  http://localhost:8081
 * (monta ../arquitetura e ../adr ao lado do workspace e sobe o mermaid.ink local usado pelos diagramas da documentação)
 */
workspace "CashFlow" "Registro de lançamentos e posição consolidada de saldo — banco digital" {

    !identifiers hierarchical

    configuration {
        scope softwaresystem
    }

    model {
        operador = person "Operador de Back-office" "Registra créditos/débitos e abre contas."
        analista = person "Analista / Gestor" "Consulta saldos consolidados e relatórios diários."
        canais = softwareSystem "Canais Digitais do Banco" "App/Internet Banking e integrações que originam movimentações." "Externo"
        siem = softwareSystem "SIEM / Observabilidade Corporativa" "Recebe logs, métricas e traces (OTLP)." "Externo"
        reguladores = softwareSystem "Contabilidade / Relatórios Regulatórios" "Consome posições consolidadas." "Externo"

        cashflow = softwareSystem "CashFlow" "Registra movimentações financeiras e fornece a posição consolidada de saldo ao longo do tempo." {
            !docs ../arquitetura
            // Formato MADR: status/data vêm do front matter YAML de cada ADR
            !adrs ../adr madr

            web = container "Console Web" "SPA de operação (HTML/JS) servida por Nginx; access token só em memória." "Nginx + JavaScript" "Web"
            edge = container "Front Door + WAF" "TLS, OWASP DRS 2.1, Bot Manager, rate limit por IP, geo-filtro." "Azure Front Door Premium" "Infra"
            gateway = container "API Gateway" "Roteamento, rate limiting por cliente, CORS, limites de payload, trava de origem." "ASP.NET Core 10 + YARP"
            identity = container "Serviço de Identidade" "Autenticação, emissão de JWT RS256, refresh token rotativo, JWKS." "ASP.NET Core 10"
            identityDb = container "Banco Identidade" "Usuários (hash PBKDF2) e refresh tokens (hash SHA-256)." "PostgreSQL 16+" "Database"
            transactions = container "Serviço de Lançamentos" "Fonte da verdade: contas e lançamentos; invariante de saldo; Outbox." "ASP.NET Core 10"
            transactionsDb = container "Banco Lançamentos" "Contas (PII criptografada), lançamentos append-only, outbox." "PostgreSQL 16+" "Database"
            broker = container "Broker de Eventos" "Exchange topic, filas quorum, retry com TTL e DLQ." "RabbitMQ 4.1" "Queue"
            consolidation = container "Serviço de Consolidado" "Consome eventos (Inbox/idempotente), projeta saldos diários e responde saldo em qualquer instante." "ASP.NET Core 10"
            consolidationDb = container "Banco Consolidado" "Inbox, lançamentos projetados e consolidados diários." "PostgreSQL 16+" "Database"
        }

        operador -> cashflow.web "Usa" "HTTPS"
        analista -> cashflow.web "Usa" "HTTPS"
        canais -> cashflow.edge "Registra lançamentos / consulta saldo" "HTTPS + JWT"
        cashflow.web -> cashflow.edge "Chamadas de API (same-origin)" "HTTPS"
        cashflow.edge -> cashflow.gateway "Encaminha tráfego inspecionado" "HTTPS + X-Azure-FDID"
        cashflow.gateway -> cashflow.identity "/auth, /.well-known" "HTTP"
        cashflow.gateway -> cashflow.transactions "/api/v1/accounts/**" "HTTP"
        cashflow.gateway -> cashflow.consolidation "/balance, /daily-balances" "HTTP"
        cashflow.transactions -> cashflow.identity "Obtém JWKS (chave pública)" "HTTP"
        cashflow.consolidation -> cashflow.identity "Obtém JWKS (chave pública)" "HTTP"
        cashflow.identity -> cashflow.identityDb "Lê/escreve" "TCP/TLS"
        cashflow.transactions -> cashflow.transactionsDb "Lê/escreve (transação única: lançamento + outbox)" "TCP/TLS"
        cashflow.transactions -> cashflow.broker "Publica eventos (publisher confirms)" "AMQP"
        cashflow.broker -> cashflow.consolidation "Entrega eventos (ack manual)" "AMQP"
        cashflow.consolidation -> cashflow.consolidationDb "Inbox + projeção (transação única)" "TCP/TLS"
        cashflow -> siem "Exporta telemetria" "OTLP"
        reguladores -> cashflow.edge "Consulta consolidados" "HTTPS + JWT"
    }

    views {
        // Blocos ```mermaid da documentação viram imagens geradas pelo mermaid.ink local (docker-compose.yml)
        properties {
            "mermaid.url" "http://localhost:3000"
            "mermaid.format" "svg"
        }

        systemContext cashflow "C1-Contexto" {
            include *
            autolayout lr
        }

        container cashflow "C2-Containers" {
            include *
            autolayout lr
        }

        styles {
            element "Person" {
                shape Person
                background #0b5cad
                color #ffffff
            }
            element "Software System" {
                background #1168bd
                color #ffffff
            }
            element "Externo" {
                background #8a8a8a
            }
            element "Container" {
                background #438dd5
                color #ffffff
            }
            element "Database" {
                shape Cylinder
            }
            element "Queue" {
                shape Pipe
            }
            element "Web" {
                shape WebBrowser
            }
            element "Infra" {
                background #5a6b7d
            }
        }
    }

    !plugin com.structurizr.dsl.plugin.documentation.Mermaid
}
