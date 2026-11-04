---
status: accepted
date: 2026-10-29
---

# ADR-0005 — JWT RS256 de curta duração + refresh token opaco rotativo

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
Pontos de atenção levantados na avaliação anterior: credenciais estáticas em texto puro, ausência de persistência de autenticação, falta de refresh tokens e de criptografia adequada. Em um banco, o vazamento de um token não pode conceder acesso prolongado, e os serviços não devem compartilhar segredo de assinatura.

## Decisão
- **Access token JWT assinado com RS256** (RSA 3072 bits), validade de **10 minutos**, claims mínimas (`sub`, `name`, `role`, `scope`, `jti`), `aud=cashflow-api`, `iss=cashflow-identity`.
- **Apenas o Identity possui a chave privada.** Lançamentos e Consolidado validam pela chave pública obtida via discovery/JWKS (`/.well-known/openid-configuration`). Algoritmos aceitos restritos a RS256 (evita *alg confusion*), `ClockSkew` de 30 s.
- **Autorização por escopo** (`transactions:write`, `transactions:read`, `balances:read`) com política *fallback* "autenticado" — endpoints novos nascem protegidos.
- **Refresh token opaco** (256 bits aleatórios), **persistido só como hash SHA-256**, validade de 7 dias, **rotação a cada uso** e **detecção de reuso**: apresentar um token já rotacionado revoga toda a família (sessão) — padrão do OAuth 2.0 Security BCP.
- Clientes web recebem o refresh token **somente** em cookie `HttpOnly; Secure; SameSite=Strict; Path=/auth`; o access token fica apenas em memória (mitiga XSS). Clientes de API recebem no corpo.
- Senhas com **PBKDF2-HMAC-SHA512 (ASP.NET Core Identity PasswordHasher v3)**; lockout após 5 falhas por 15 min; resposta idêntica para usuário inexistente e senha errada (anti-enumeração, inclusive em tempo de resposta).
- Logout (`/auth/revoke`) revoga a cadeia inteira.
- Chave de assinatura fornecida por segredo (Docker secret local; Key Vault em nuvem). Rotação: publicar nova chave no JWKS antes de passar a assinar com ela (o `kid` é o thumbprint RFC 7638).

## Alternativas consideradas
- **HS256 com segredo compartilhado:** qualquer serviço comprometido poderia emitir tokens; rotação exige redeploy coordenado.
- **Sessão stateful/cookie de sessão:** acopla APIs a um store de sessão; não atende bem clientes de API.
- **Entra ID / Keycloak:** recomendado para produção corporativa (SSO, MFA, federação) — ver ADR-0008; o desenho dos serviços (validação via OIDC discovery) permite a troca apenas por configuração.

## Consequências
- ➕ Vazamento de access token tem janela máxima de 10 min; roubo de refresh token é detectável.
- ➕ Nenhum segredo de assinatura fora do Identity.
- ➖ Revogação imediata de access tokens não é possível sem introspecção (aceito pela curta duração; lista de `jti` revogados é evolução possível).
- Testes: `CashFlow.Identity.Tests` (rotação, reuso, lockout, cookie endurecido, JWKS) e `AuthorizationTests` (401/403, expirado, audiência errada, chave desconhecida).
