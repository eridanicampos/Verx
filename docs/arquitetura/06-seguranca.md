# 6. Arquitetura de Segurança

## 6.1 Modelo de ameaças (STRIDE resumido)

| Ameaça | Exemplo | Controle |
|---|---|---|
| **S**poofing | Token forjado / senha vazada | JWT RS256 validado por JWKS (só o Identity assina); algoritmos restritos; lockout; rate limit de login; PBKDF2 |
| **T**ampering | Alterar valor em trânsito ou no banco | TLS (Front Door → apps; `require_secure_transport` no PostgreSQL); lançamentos imutáveis; AES-GCM autenticado para PII |
| **R**epudiation | "Não fiz esse lançamento" | `sub` do token + `traceparent` em logs estruturados; lançamentos append-only com `occurred_at` do servidor |
| **I**nformation disclosure | Vazamento de CPF / segredos | PII cifrada em campo + mascarada na resposta; eventos sem PII; segredos em Key Vault/Docker secrets; `Cache-Control: no-store` |
| **D**enial of service | Flood, força bruta | WAF (rate limit por IP, Bot Manager), gateway (token bucket por IP, política de auth), limites de payload e headers, rate limit por cliente nos serviços |
| **E**levation of privilege | Analista registrando lançamento | Autorização por escopo; política fallback "autenticado"; identidades gerenciadas com menor privilégio |

## 6.2 Gestão de segredos (sem credenciais em texto puro)

| Ambiente | Mecanismo |
|---|---|
| Local / CI | `scripts/generate-secrets.sh|ps1` gera senhas aleatórias (32+ chars), chaves AES-256, chave RSA 3072 e connection strings em `secrets/` (no `.gitignore`); montados como **Docker secrets** em `/run/secrets`; a aplicação lê via `AddKeyPerFile` |
| Nuvem | **Azure Key Vault** (RBAC, soft delete 90 d, purge protection em prod); cada app acessa com **identidade gerenciada própria** (role *Key Vault Secrets User*); Container Apps resolvem `key_vault_secret_id` sem expor valores; segredos com data de expiração (rotação a cada 180 dias) |

- `appsettings.json` não contém nenhuma senha/chave; serviços **falham na subida** se um segredo obrigatório faltar (fail fast).
- Usuários de banco distintos por serviço (menor privilégio); o usuário administrador só é usado pelo job de bootstrap.
- RabbitMQ: usuário `guest` não é usado; credenciais via arquivo de configuração montado como segredo.

## 6.3 Autenticação e persistência de sessão
Detalhado no [ADR-0005](../adr/0005-autenticacao-jwt-rs256-e-refresh-token.md):
- Access token 10 min (RS256) · refresh token 7 dias, opaco, **hash SHA-256 no banco**, **rotação** e **detecção de reuso** com revogação da família.
- Web: refresh token em cookie `HttpOnly; Secure; SameSite=Strict; Path=/auth`; ao recarregar a página a sessão é restaurada via `/auth/refresh` (persistência de autenticação sem `localStorage`).
- Logout revoga a cadeia inteira.

## 6.4 Criptografia

| Dado | Em trânsito | Em repouso |
|---|---|---|
| Todos | TLS 1.2+ (Front Door, PostgreSQL `require_secure_transport`) | Criptografia de disco do Azure (AES-256, chaves gerenciadas) |
| CPF/CNPJ | — | **AES-256-GCM em nível de campo** com `keyId` (rotação sem recriptografar tudo) + **blind index HMAC-SHA256** para unicidade/busca |
| Senhas | — | PBKDF2-HMAC-SHA512 (100 mil iterações, salt por usuário) |
| Refresh tokens | — | SHA-256 |
| Chave JWT | — | Key Vault (evolução: assinatura em Key Vault/HSM sem exportar chave) |

## 6.5 Proteção ativa
Camadas descritas no [ADR-0014](../adr/0014-api-gateway-e-protecao-de-borda.md): WAF (DRS 2.1, Bot Manager, rate limit, geo), gateway (token bucket por IP, limite de auth, payload, origem obrigatória via Front Door), serviços (rate limit por `sub`) e Identity (rate limit + lockout). Testado em `GatewayTests` e `LoginRateLimitTests`.

## 6.6 Segurança de aplicação e supply chain
- Cabeçalhos: `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, CSP restritiva na UI, HSTS no gateway sob HTTPS; banner `Server` removido.
- Validação de entrada em todos os comandos (FluentValidation) e limites de tamanho.
- Containers não-root, filesystem somente leitura, `cap_drop: ALL`.
- Dependências: versões fixadas + lock files; **NuGetAudit (inclui transitivas, também nos projetos de teste)** quebra o build em CVE; `dotnet list package --vulnerable --include-transitive` no CI; scan Trivy das imagens; verificação de licenças.

## 6.7 LGPD e conformidade
- **Minimização:** o Consolidado e os eventos não recebem dados pessoais.
- **Finalidade e acesso:** escopos por perfil; documento sempre mascarado nas respostas.
- **Residência:** região Brazil South.
- **Direito de eliminação:** dados pessoais concentrados em `accounts` (crypto-shredding possível destruindo a chave da versão, mantendo os lançamentos contábeis anonimizados).

## 6.8 Lacunas conhecidas (próximos passos)
- MFA/SSO via IdP corporativo (ADR-0008).
- mTLS entre serviços (habilitável no Container Apps) e Private Link Front Door → ambiente interno.
- Private Endpoint para Key Vault e ACR.
- Assinatura de imagens (cosign) e SBOM no pipeline.
