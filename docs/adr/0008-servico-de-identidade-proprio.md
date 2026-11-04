---
status: accepted
date: 2026-10-29
---

# ADR-0008 — Serviço de identidade próprio e substituível

- **Status:** Aceito (com plano de substituição)
- **Data:** 2026-10-29

## Contexto
O case precisa ser executável localmente e demonstrar mecanismos de autenticação (refresh token, rotação, lockout). Em produção, um banco tipicamente centraliza identidade em um IdP corporativo (Entra ID, Keycloak, Okta) com MFA e SSO.

## Decisão
- Implementar um **Identity mínimo** (`CashFlow.Identity.Api`) que segue os padrões OIDC necessários aos consumidores: discovery, JWKS, JWT RS256, escopos.
- Por ser um **subdomínio de suporte pequeno**, fica em **um único projeto com pastas por camada** (Domain/Application/Infrastructure/Endpoints), em vez de 4 projetos — a separação física traria custo sem benefício proporcional.
- Os serviços de negócio dependem apenas de **configuração OIDC** (`Auth:Authority`, `Issuer`, `Audience`). Trocar pelo Entra ID/Keycloak = mudar configuração e mapear escopos/roles; nenhum código de negócio muda.

## Alternativas consideradas
- **Keycloak no compose:** realista, porém pesado (Java, ~1 GB RAM) e desvia o foco da avaliação.
- **Duende IdentityServer:** licença comercial para receita acima do limite community — viola ADR-0007.
- **OpenIddict (Apache-2.0):** boa opção se for necessário suportar OAuth2 completo (client credentials, authorization code + PKCE); candidato natural de evolução.

## Consequências
- ➕ Executa localmente com poucos recursos; demonstra os controles exigidos.
- ➖ Não implementa MFA, federação nem fluxos OAuth2 completos — **não deve ir a produção sem a substituição planejada** (roadmap em `docs/arquitetura/08-oportunidades-e-migracao.md`).
