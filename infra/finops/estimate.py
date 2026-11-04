#!/usr/bin/env python3
"""
Modelo de custos (FinOps) da plataforma CashFlow.
Fonte de verdade: cost-model.csv (preços de lista pay-as-you-go, região Brazil South, referência out/2026).
Gera docs/finops/estimativa-custos.md com totais por ambiente/categoria, cenários de crescimento,
custo unitário por transação e otimizações. Uso: python3 infra/finops/estimate.py
"""
import csv
import os
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "docs", "finops", "estimativa-custos.md")

# Premissas de volume (alinhadas aos requisitos não funcionais e ao teste de carga)
MONTHLY_TRANSACTIONS_PROD = 20 * 3600 * 10 * 22  # 20 lanç./s em 10h úteis × 22 dias ≈ 15,8 M/mês
FX_BRL = 5.40  # câmbio de referência para leitura executiva

# Otimizações com impacto estimado (percentual sobre a linha)
SAVINGS = [
    ("prod", "Container Apps", "Azure Savings Plan for Compute 1 ano", 0.15),
    ("prod", "PostgreSQL Flexible Server", "Instância reservada 1 ano (somente computação)", 0.35),
    ("prod", "Log Analytics + Application Insights", "Tabelas Basic Logs para logs verbosos + amostragem adaptativa", 0.30),
    ("dev", "Azure Front Door", "Front Door Standard em dev (WAF gerenciado só em prod)", 0.89),
    ("dev", "Container Apps", "Agendar desligamento do RabbitMQ fora do horário comercial", 0.40),
]

# Elasticidade por categoria quando o volume cresce N vezes (fração do custo que é variável)
VARIABLE_SHARE = {"Borda": 0.35, "Computação": 0.60, "Dados": 0.30, "Mensageria": 0.20, "Segurança": 0.10,
                  "Observabilidade": 0.80, "Plataforma": 0.0}


def load():
    with open(os.path.join(HERE, "cost-model.csv"), encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    for r in rows:
        r["monthly"] = float(r["quantity"]) * float(r["unit_price_usd"])
    return rows


def num(v, decimals=0):
    """Formato pt-BR: 1.234.567,89"""
    return f"{v:,.{decimals}f}".replace(",", "X").replace(".", ",").replace("X", ".")


def money(v):
    return "US$ " + num(v, 2)


def brl(v):
    return "R$ " + num(v, 0)


def unit_price(v):
    text = f"{v:.7f}".rstrip("0").rstrip(".")
    return text.replace(".", ",")


def main():
    rows = load()
    by_env = defaultdict(float)
    by_env_cat = defaultdict(lambda: defaultdict(float))
    by_env_service = defaultdict(lambda: defaultdict(float))
    for r in rows:
        by_env[r["environment"]] += r["monthly"]
        by_env_cat[r["environment"]][r["category"]] += r["monthly"]
        by_env_service[r["environment"]][r["service"]] += r["monthly"]

    lines = []
    w = lines.append
    w("# Estimativa de custos de nuvem (FinOps)")
    w("")
    w("> Gerado por `infra/finops/estimate.py` a partir de `infra/finops/cost-model.csv`. "
      "Preços de lista pay-as-you-go da região **Brazil South** (referência out/2026), sem impostos. "
      "Validar no Azure Pricing Calculator antes de compor orçamento — o modelo existe para tornar as premissas explícitas e auditáveis.")
    w("")
    w("## Resumo mensal")
    w("")
    w("| Ambiente | Custo mensal (USD) | Custo mensal (BRL ref.) | Custo anual (USD) | Orçamento no Terraform |")
    w("|---|---:|---:|---:|---:|")
    budgets = {"prod": 2600, "dev": 700}
    for env in ("prod", "dev"):
        w(f"| {env} | {money(by_env[env])} | {brl(by_env[env] * FX_BRL)} | {money(by_env[env] * 12)} | {money(budgets[env])} |")
    total = sum(by_env.values())
    w(f"| **Total** | **{money(total)}** | **{brl(total * FX_BRL)}** | **{money(total * 12)}** | |")
    w("")
    w("O orçamento (`azurerm_consumption_budget_resource_group`) é ~25% acima da estimativa e dispara alertas em 80% (real) e 100% (previsto).")
    w("")

    for env in ("prod", "dev"):
        w(f"## Detalhamento — {env}")
        w("")
        w("| Categoria | Serviço | Item | SKU | Quantidade | Unidade | Preço unit. (USD) | Mensal (USD) | % |")
        w("|---|---|---|---|---:|---|---:|---:|---:|")
        for r in sorted((r for r in rows if r["environment"] == env), key=lambda r: -r["monthly"]):
            pct = r["monthly"] / by_env[env] * 100
            w(f"| {r['category']} | {r['service']} | {r['resource']} | {r['sku']} | {num(float(r['quantity']))} | {r['unit']} | {unit_price(float(r['unit_price_usd']))} | {money(r['monthly'])} | {num(pct, 1)}% |")
        w("")
        w("| Categoria | Mensal (USD) | % |")
        w("|---|---:|---:|")
        for cat, v in sorted(by_env_cat[env].items(), key=lambda kv: -kv[1]):
            w(f"| {cat} | {money(v)} | {num(v / by_env[env] * 100, 1)}% |")
        w("")

    w("## Custo unitário (produção)")
    w("")
    per_k = by_env["prod"] / (MONTHLY_TRANSACTIONS_PROD / 1000)
    w(f"Premissa: {num(MONTHLY_TRANSACTIONS_PROD)} lançamentos/mês (20/s em 10h úteis × 22 dias) e leitura do consolidado a 50 rps no pico. Câmbio de referência: R$ {num(FX_BRL, 2)}.")
    w("")
    w(f"- **Custo por mil lançamentos:** {money(per_k)}")
    w(f"- **Custo por lançamento:** US$ {unit_price(by_env['prod'] / MONTHLY_TRANSACTIONS_PROD)}")
    w("- O custo é dominado por capacidade **fixa** (réplicas mínimas para HA, HA do banco e Front Door Premium); o custo marginal por transação é baixo.")
    w("")

    w("## Cenários de crescimento (produção)")
    w("")
    w("| Volume | Mensal (USD) | Custo por mil lançamentos |")
    w("|---|---:|---:|")
    for factor in (1, 2, 5, 10):
        cost = 0.0
        for cat, v in by_env_cat["prod"].items():
            share = VARIABLE_SHARE.get(cat, 0.5)
            cost += v * (1 - share) + v * share * factor
        w(f"| {factor}× ({num(MONTHLY_TRANSACTIONS_PROD * factor / 1e6, 1)} M/mês) | {money(cost)} | {money(cost / (MONTHLY_TRANSACTIONS_PROD * factor / 1000))} |")
    w("")
    w("Elasticidade assumida (fração variável por categoria): " + ", ".join(f"{k} {int(v * 100)}%" for k, v in VARIABLE_SHARE.items()) + ".")
    w("")

    w("## Otimizações mapeadas")
    w("")
    w("| Ambiente | Alvo | Ação | Redução estimada (USD/mês) |")
    w("|---|---|---|---:|")
    saved = defaultdict(float)
    for env, service, action, pct in SAVINGS:
        base = by_env_service[env].get(service, 0) if service != "Container Apps" or env == "prod" else \
            sum(r["monthly"] for r in rows if r["environment"] == env and r["service"] == service and "RabbitMQ" in r["resource"])
        value = base * pct
        saved[env] += value
        w(f"| {env} | {service} | {action} | {money(value)} |")
    w("")
    for env in ("prod", "dev"):
        w(f"- **{env}:** de {money(by_env[env])} para ~{money(by_env[env] - saved[env])} (-{num(saved[env] / by_env[env] * 100)}%).")
    w("")
    w("## Governança FinOps aplicada no IaC")
    w("")
    w("- Tags obrigatórias em todos os recursos: `project`, `environment`, `cost-center`, `owner`, `managed-by` (showback/chargeback).")
    w("- Orçamento por resource group com alertas automáticos (`modules/platform`).")
    w("- Teto diário de ingestão no Log Analytics e amostragem no Application Insights.")
    w("- Escala a zero das APIs em dev; escala por fila (KEDA) no Consolidado em vez de superdimensionar.")
    w("- Revisão mensal: comparar custo real × modelo (`cost-model.csv` versionado junto do código).")
    w("")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    for env in ("prod", "dev"):
        print(f"{env}: {money(by_env[env])}/mês")
    print(f"Relatório: {os.path.relpath(OUT, ROOT)}")


if __name__ == "__main__":
    main()
