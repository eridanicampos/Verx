#!/usr/bin/env bash
# Gera todos os segredos locais em ./secrets (ignorado pelo git).
# Nenhuma credencial é versionada no repositório: em nuvem os mesmos nomes
# vêm do Azure Key Vault (ver infra/terraform e docs/seguranca).
#
# Uso: ./scripts/generate-secrets.sh [--force] [--db-host postgres] [--mq-host rabbitmq]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/secrets"
FORCE=0
DB_HOST="postgres"
MQ_HOST="rabbitmq"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --force) FORCE=1 ;;
    --db-host) DB_HOST="$2"; shift ;;
    --mq-host) MQ_HOST="$2"; shift ;;
    *) echo "Parâmetro desconhecido: $1" >&2; exit 1 ;;
  esac
  shift
done

if [[ -d "$OUT" && $FORCE -eq 0 ]]; then
  echo "secrets/ já existe. Use --force para regenerar (invalida dados/tokens existentes)."
  exit 0
fi

command -v openssl >/dev/null || { echo "openssl é necessário." >&2; exit 1; }

rand() { openssl rand -base64 48 | tr -d '/+=\n' | cut -c1-"${1:-32}"; }
key32() { openssl rand -base64 32 | tr -d '\n'; }

rm -rf "$OUT"
mkdir -p "$OUT"/{infra,transactions,consolidation,identity}
umask 077

PG_ADMIN=$(rand); PG_TX=$(rand); PG_CO=$(rand); PG_ID=$(rand)
MQ_PASS=$(rand)
OPERATOR_PASS="Op-$(rand 20)"
ANALYST_PASS="An-$(rand 20)"

# Infraestrutura
printf '%s' "$PG_ADMIN" > "$OUT/infra/postgres_admin_password"
printf '%s' "$PG_TX"    > "$OUT/infra/pg_transactions_password"
printf '%s' "$PG_CO"    > "$OUT/infra/pg_consolidation_password"
printf '%s' "$PG_ID"    > "$OUT/infra/pg_identity_password"
cat > "$OUT/infra/rabbitmq.conf" <<CONF
default_user = cashflow
default_pass = $MQ_PASS
loopback_users = none
CONF

# Lançamentos
printf 'Host=%s;Port=5432;Database=cashflow_transactions;Username=svc_transactions;Password=%s;Maximum Pool Size=60' "$DB_HOST" "$PG_TX" \
  > "$OUT/transactions/ConnectionStrings__Transactions"
printf '%s' "$MQ_PASS" > "$OUT/transactions/RabbitMq__Password"
printf '%s' "$MQ_HOST" > "$OUT/transactions/RabbitMq__HostName"
key32 > "$OUT/transactions/Pii__EncryptionKeys__k1"
key32 > "$OUT/transactions/Pii__BlindIndexKey"

# Consolidado
printf 'Host=%s;Port=5432;Database=cashflow_consolidation;Username=svc_consolidation;Password=%s;Maximum Pool Size=60' "$DB_HOST" "$PG_CO" \
  > "$OUT/consolidation/ConnectionStrings__Consolidation"
printf '%s' "$MQ_PASS" > "$OUT/consolidation/RabbitMq__Password"
printf '%s' "$MQ_HOST" > "$OUT/consolidation/RabbitMq__HostName"

# Identidade
printf 'Host=%s;Port=5432;Database=cashflow_identity;Username=svc_identity;Password=%s' "$DB_HOST" "$PG_ID" \
  > "$OUT/identity/ConnectionStrings__Identity"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out "$OUT/identity/Jwt__SigningKeyPem" 2>/dev/null
printf '%s' "$OPERATOR_PASS" > "$OUT/identity/Identity__SeedUsers__0__Password"
printf '%s' "$ANALYST_PASS"  > "$OUT/identity/Identity__SeedUsers__1__Password"

chmod -R a+rX "$OUT"   # leitura pelos usuários não-root dos containers
echo "Segredos gerados em $OUT"
echo "Usuário 'operador' (escrita) — senha em secrets/identity/Identity__SeedUsers__0__Password"
echo "Usuário 'analista' (leitura) — senha em secrets/identity/Identity__SeedUsers__1__Password"
