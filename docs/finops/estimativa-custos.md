# Estimativa de custos de nuvem (FinOps)

> Gerado por `infra/finops/estimate.py` a partir de `infra/finops/cost-model.csv`. Preços de lista pay-as-you-go da região **Brazil South** (referência out/2026), sem impostos. Validar no Azure Pricing Calculator antes de compor orçamento — o modelo existe para tornar as premissas explícitas e auditáveis.

## Resumo mensal

| Ambiente | Custo mensal (USD) | Custo mensal (BRL ref.) | Custo anual (USD) | Orçamento no Terraform |
|---|---:|---:|---:|---:|
| prod | US$ 2.063,33 | R$ 11.142 | US$ 24.759,96 | US$ 2.600,00 |
| dev | US$ 630,79 | R$ 3.406 | US$ 7.569,45 | US$ 700,00 |
| **Total** | **US$ 2.694,12** | **R$ 14.548** | **US$ 32.329,41** | |

O orçamento (`azurerm_consumption_budget_resource_group`) é ~25% acima da estimativa e dispara alertas em 80% (real) e 100% (previsto).

## Detalhamento — prod

| Categoria | Serviço | Item | SKU | Quantidade | Unidade | Preço unit. (USD) | Mensal (USD) | % |
|---|---|---|---|---:|---|---:|---:|---:|
| Computação | Container Apps | vCPU ativa (identity 2×0.5; transactions 2×1; consolidation 2×1; gateway 2×0.5; web 2×0.25; rabbitmq 1×1) | Consumption | 19.710.000 | vCPU-s | 0,0000336 | US$ 662,26 | 32,1% |
| Dados | PostgreSQL Flexible Server | Computação GP 2 vCores (primário + standby HA zona redundante) | GP_Standard_D2ds_v5 | 2 | instância-mês | 190 | US$ 380,00 | 18,4% |
| Borda | Azure Front Door | Perfil Premium (base) | Premium_AzureFrontDoor | 1 | mês | 330 | US$ 330,00 | 16,0% |
| Observabilidade | Log Analytics + Application Insights | Ingestão de logs/traces | PerGB2018 | 85 | GB | 3,45 | US$ 293,25 | 14,2% |
| Computação | Container Apps | Memória ativa | Consumption | 39.420.000 | GiB-s | 0,0000042 | US$ 165,56 | 8,0% |
| Borda | Azure Front Door | Requisições | Premium | 5.200 | 10 mil req | 0,0125 | US$ 65,00 | 3,2% |
| Dados | PostgreSQL Flexible Server | Armazenamento SSD P10 (primário + standby) | 128 GiB | 256 | GB-mês | 0,17 | US$ 43,52 | 2,1% |
| Dados | PostgreSQL Flexible Server | Backup geo-redundante (PITR 35 dias) | GRS | 128 | GB-mês | 0,2 | US$ 25,60 | 1,2% |
| Observabilidade | Log Analytics | Retenção além de 31 dias (até 90) | PerGB2018 | 180 | GB-mês | 0,12 | US$ 21,60 | 1,0% |
| Computação | Container Apps | Requisições HTTP | Consumption | 50 | milhão req | 0,4 | US$ 20,00 | 1,0% |
| Plataforma | Container Registry | Registro de imagens | Standard | 1 | mês | 20 | US$ 20,00 | 1,0% |
| Borda | Azure Front Door | Transferência de dados (saída) | Zona América do Sul | 110 | GB | 0,18 | US$ 19,80 | 1,0% |
| Segurança | Defender for Cloud (recomendado) | Defender for open-source relational DBs | Plano por instância | 1 | instância-mês | 15 | US$ 15,00 | 0,7% |
| Mensageria | Storage Account (Azure Files) | Volume persistente RabbitMQ | ZRS 20 GiB | 20 | GB-mês | 0,24 | US$ 4,80 | 0,2% |
| Segurança | Key Vault | Operações de segredo | Standard | 150 | 10 mil operações | 0,03 | US$ 4,50 | 0,2% |
| Computação | Container Apps | Franquia gratuita mensal (vCPU + memória) | Consumption | 1 | mês | -7,56 | US$ -7,56 | -0,4% |

| Categoria | Mensal (USD) | % |
|---|---:|---:|
| Computação | US$ 840,26 | 40,7% |
| Dados | US$ 449,12 | 21,8% |
| Borda | US$ 414,80 | 20,1% |
| Observabilidade | US$ 314,85 | 15,3% |
| Plataforma | US$ 20,00 | 1,0% |
| Segurança | US$ 19,50 | 0,9% |
| Mensageria | US$ 4,80 | 0,2% |

## Detalhamento — dev

| Categoria | Serviço | Item | SKU | Quantidade | Unidade | Preço unit. (USD) | Mensal (USD) | % |
|---|---|---|---|---:|---|---:|---:|---:|
| Borda | Azure Front Door | Perfil Premium (base) | Premium_AzureFrontDoor | 1 | mês | 330 | US$ 330,00 | 52,3% |
| Computação | Container Apps | vCPU RabbitMQ (sempre ativo) | Consumption | 2.628.000 | vCPU-s | 0,0000336 | US$ 88,30 | 14,0% |
| Computação | Container Apps | vCPU ativa (APIs escalam a zero; ~8h × 22 dias) | Consumption | 2.534.400 | vCPU-s | 0,0000336 | US$ 85,16 | 13,5% |
| Computação | Container Apps | Memória ativa | Consumption | 12.321.600 | GiB-s | 0,0000042 | US$ 51,75 | 8,2% |
| Observabilidade | Log Analytics + Application Insights | Ingestão | PerGB2018 | 10 | GB | 3,45 | US$ 34,50 | 5,5% |
| Plataforma | Container Registry | Registro de imagens | Standard | 1 | mês | 20 | US$ 20,00 | 3,2% |
| Dados | PostgreSQL Flexible Server | Computação burstable 1 vCore | B_Standard_B1ms | 1 | instância-mês | 16 | US$ 16,00 | 2,5% |
| Dados | PostgreSQL Flexible Server | Armazenamento | 32 GiB | 32 | GB-mês | 0,17 | US$ 5,44 | 0,9% |
| Borda | Azure Front Door | Requisições + dados | Premium | 1 | mês | 5 | US$ 5,00 | 0,8% |
| Mensageria | Storage Account (Azure Files) | Volume RabbitMQ | LRS 20 GiB | 20 | GB-mês | 0,08 | US$ 1,60 | 0,3% |
| Segurança | Key Vault | Operações | Standard | 20 | 10 mil operações | 0,03 | US$ 0,60 | 0,1% |
| Computação | Container Apps | Franquia gratuita mensal | Consumption | 1 | mês | -7,56 | US$ -7,56 | -1,2% |

| Categoria | Mensal (USD) | % |
|---|---:|---:|
| Borda | US$ 335,00 | 53,1% |
| Computação | US$ 217,65 | 34,5% |
| Observabilidade | US$ 34,50 | 5,5% |
| Dados | US$ 21,44 | 3,4% |
| Plataforma | US$ 20,00 | 3,2% |
| Mensageria | US$ 1,60 | 0,3% |
| Segurança | US$ 0,60 | 0,1% |

## Custo unitário (produção)

Premissa: 15.840.000 lançamentos/mês (20/s em 10h úteis × 22 dias) e leitura do consolidado a 50 rps no pico. Câmbio de referência: R$ 5,40.

- **Custo por mil lançamentos:** US$ 0,13
- **Custo por lançamento:** US$ 0,0001303
- O custo é dominado por capacidade **fixa** (réplicas mínimas para HA, HA do banco e Front Door Premium); o custo marginal por transação é baixo.

## Cenários de crescimento (produção)

| Volume | Mensal (USD) | Custo por mil lançamentos |
|---|---:|---:|
| 1× (15,8 M/mês) | US$ 2.063,33 | US$ 0,13 |
| 2× (31,7 M/mês) | US$ 3.102,19 | US$ 0,10 |
| 5× (79,2 M/mês) | US$ 6.218,78 | US$ 0,08 |
| 10× (158,4 M/mês) | US$ 11.413,09 | US$ 0,07 |

Elasticidade assumida (fração variável por categoria): Borda 35%, Computação 60%, Dados 30%, Mensageria 20%, Segurança 10%, Observabilidade 80%, Plataforma 0%.

## Otimizações mapeadas

| Ambiente | Alvo | Ação | Redução estimada (USD/mês) |
|---|---|---|---:|
| prod | Container Apps | Azure Savings Plan for Compute 1 ano | US$ 126,04 |
| prod | PostgreSQL Flexible Server | Instância reservada 1 ano (somente computação) | US$ 157,19 |
| prod | Log Analytics + Application Insights | Tabelas Basic Logs para logs verbosos + amostragem adaptativa | US$ 87,97 |
| dev | Azure Front Door | Front Door Standard em dev (WAF gerenciado só em prod) | US$ 298,15 |
| dev | Container Apps | Agendar desligamento do RabbitMQ fora do horário comercial | US$ 35,32 |

- **prod:** de US$ 2.063,33 para ~US$ 1.692,12 (-18%).
- **dev:** de US$ 630,79 para ~US$ 297,32 (-53%).

## Governança FinOps aplicada no IaC

- Tags obrigatórias em todos os recursos: `project`, `environment`, `cost-center`, `owner`, `managed-by` (showback/chargeback).
- Orçamento por resource group com alertas automáticos (`modules/platform`).
- Teto diário de ingestão no Log Analytics e amostragem no Application Insights.
- Escala a zero das APIs em dev; escala por fila (KEDA) no Consolidado em vez de superdimensionar.
- Revisão mensal: comparar custo real × modelo (`cost-model.csv` versionado junto do código).

