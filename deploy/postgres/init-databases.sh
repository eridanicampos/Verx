#!/usr/bin/env bash
# Database-per-service: cada microsserviço tem seu banco e seu usuário (menor privilégio).
# As senhas são lidas dos Docker secrets montados em /run/secrets.
set -euo pipefail

create() {
  local db="$1" user="$2" secret="$3"
  local pass
  pass="$(cat "/run/secrets/$secret")"
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
       -v db="$db" -v usr="$user" -v pass="$pass" <<'SQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'usr', :'pass') \gexec
SELECT format('CREATE DATABASE %I OWNER %I', :'db', :'usr') \gexec
SELECT format('REVOKE ALL ON DATABASE %I FROM PUBLIC', :'db') \gexec
SQL
}

create cashflow_transactions  svc_transactions  pg_transactions_password
create cashflow_consolidation svc_consolidation pg_consolidation_password
create cashflow_identity      svc_identity      pg_identity_password
