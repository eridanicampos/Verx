# Infraestrutura como Código (Terraform · Azure)

```
infra/terraform/
├── modules/
│   ├── platform/        # composição: RG, segredos derivados, Front Door profile, orçamento
│   ├── network/         # VNet, sub-redes delegadas, NSG do banco, DNS privado
│   ├── observability/   # Log Analytics (teto diário), App Insights (amostragem), action group
│   ├── keyvault/        # Key Vault RBAC, identidades gerenciadas por serviço, segredos gerados
│   ├── postgres/        # Flexible Server privado, HA zona redundante, PITR, alerta de CPU
│   ├── container_apps/  # ACR, ambiente ACA (VNet), apps, RabbitMQ + Azure Files, job de bootstrap do banco
│   └── edge/            # Front Door Premium, WAF (DRS 2.1, Bot Manager, rate limit, geo), rotas, diagnósticos
└── environments/
    ├── dev/             # escala a zero, sem HA, WAF em Detection
    └── prod/            # zona redundante, min 2 réplicas, HA + geo-backup, WAF Prevention + geo BR
```

## Uso

```bash
cd infra/terraform/environments/prod
cp terraform.tfvars.example terraform.tfvars   # ajustar valores (não versionado)
az login
terraform init                                 # backend azurerm com Entra ID (sem access key)
terraform plan -out tfplan
terraform apply tfplan

# Após o primeiro apply: criar usuários por serviço no PostgreSQL
az containerapp job start -n caj-db-bootstrap -g rg-cashflow-prod
```

Imagens: o pipeline de release publica `cashflow/{identity,transactions,consolidation,gateway,web}:<tag>` no ACR criado; `image_tag` seleciona a versão (deploy imutável por revisão).

## Decisões de segurança no IaC
- Nenhum segredo em variáveis/tfvars: senhas, chaves AES e RSA são **geradas** (`random_password`, `random_bytes`, `tls_private_key`) e gravadas no **Key Vault**; os apps os referenciam por `key_vault_secret_id` com identidade gerenciada. O estado Terraform contém esses valores — por isso o backend é criptografado, com acesso restrito por RBAC e sem access keys.
- PostgreSQL sem acesso público, NSG permitindo apenas a sub-rede das aplicações, TLS obrigatório.
- Apenas `ca-gateway` e `ca-web` têm ingress externo, e o Gateway exige o header `X-Azure-FDID` do perfil do Front Door (bloqueia bypass do WAF).

## Validação
`terraform fmt -check -recursive` e `terraform validate` rodam no CI (job `terraform`). Provedores fixados: azurerm `~> 5.12.0`, random `~> 3.9.1`, tls `~> 4.4.2`.

Custos: [`docs/finops/estimativa-custos.md`](../../docs/finops/estimativa-custos.md).
