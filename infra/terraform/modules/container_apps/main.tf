# Runtime: Azure Container Apps (serverless de containers com KEDA e Dapr opcional).
# Escolha justificada no ADR-0009: menor custo operacional que AKS para 5 serviços,
# autoscaling por HTTP e por profundidade de fila, revisões para blue/green.

resource "azurerm_container_registry" "this" {
  name                          = replace("cr${var.name}", "-", "")
  location                      = var.location
  resource_group_name           = var.resource_group_name
  sku                           = var.acr_sku
  admin_enabled                 = false
  public_network_access_enabled = true
  tags                          = var.tags
}

resource "azurerm_role_assignment" "acr_pull" {
  for_each             = var.identities
  scope                = azurerm_container_registry.this.id
  role_definition_name = "AcrPull"
  principal_id         = each.value.principal_id
}

resource "azurerm_container_app_environment" "this" {
  name                           = "cae-${var.name}"
  location                       = var.location
  resource_group_name            = var.resource_group_name
  log_analytics_workspace_id     = var.log_analytics_workspace_id
  infrastructure_subnet_id       = var.subnet_id
  internal_load_balancer_enabled = false
  zone_redundancy_enabled        = var.zone_redundant
  tags                           = var.tags

  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }

  lifecycle {
    ignore_changes = [infrastructure_resource_group_name]
  }
}

# ---------- Armazenamento persistente do RabbitMQ ----------
resource "azurerm_storage_account" "broker" {
  name                            = substr(replace("st${var.name}mq", "-", ""), 0, 24)
  location                        = var.location
  resource_group_name             = var.resource_group_name
  account_tier                    = "Standard"
  account_replication_type        = var.zone_redundant ? "ZRS" : "LRS"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  https_traffic_only_enabled      = true
  tags                            = var.tags
}

resource "azurerm_storage_share" "broker" {
  name               = "rabbitmq-data"
  storage_account_id = azurerm_storage_account.broker.id
  quota              = 20
}

resource "azurerm_container_app_environment_storage" "broker" {
  name                         = "rabbitmq-data"
  container_app_environment_id = azurerm_container_app_environment.this.id
  account_name                 = azurerm_storage_account.broker.name
  share_name                   = azurerm_storage_share.broker.name
  access_key                   = azurerm_storage_account.broker.primary_access_key
  access_mode                  = "ReadWrite"
}

locals {
  registry = azurerm_container_registry.this.login_server

  common_env = {
    ASPNETCORE_ENVIRONMENT                = "Production"
    APPLICATIONINSIGHTS_CONNECTION_STRING = "" # exportado via OTLP/Azure Monitor (ver docs/operacao)
    Auth__Authority                       = "http://ca-identity"
    Auth__RequireHttpsMetadata            = "false" # tráfego interno do ambiente (mTLS do ACA habilitável)
    Database__ApplyMigrationsOnStartup    = "false"
  }

  apps = {
    identity = {
      image    = "${local.registry}/cashflow/identity:${var.image_tag}"
      external = false
      min      = var.min_replicas
      max      = 3
      cpu      = 0.5
      memory   = "1Gi"
      env      = { Jwt__PublicBaseUrl = "http://ca-identity" }
      secrets = {
        ConnectionStrings__Identity      = "cs-identity"
        Jwt__SigningKeyPem               = "jwt-signing-key"
        Identity__SeedUsers__0__Password = "seed-operator-password"
        Identity__SeedUsers__1__Password = "seed-analyst-password"
      }
      http_concurrency = 50
      queue_scaling    = false
    }
    transactions = {
      image    = "${local.registry}/cashflow/transactions:${var.image_tag}"
      external = false
      min      = var.min_replicas
      max      = var.max_replicas
      cpu      = 1.0
      memory   = "2Gi"
      env      = { RabbitMq__HostName = "ca-rabbitmq" }
      secrets = {
        ConnectionStrings__Transactions = "cs-transactions"
        RabbitMq__Password              = "rabbitmq-password"
        Pii__EncryptionKeys__k1         = "pii-encryption-key-k1"
        Pii__BlindIndexKey              = "pii-blind-index-key"
      }
      http_concurrency = 80
      queue_scaling    = false
    }
    consolidation = {
      image    = "${local.registry}/cashflow/consolidation:${var.image_tag}"
      external = false
      min      = var.min_replicas
      max      = var.max_replicas
      cpu      = 1.0
      memory   = "2Gi"
      env      = { RabbitMq__HostName = "ca-rabbitmq" }
      secrets = {
        ConnectionStrings__Consolidation = "cs-consolidation"
        RabbitMq__Password               = "rabbitmq-password"
      }
      http_concurrency = 100
      queue_scaling    = true
    }
    gateway = {
      image    = "${local.registry}/cashflow/gateway:${var.image_tag}"
      external = true
      min      = var.min_replicas
      max      = var.max_replicas
      cpu      = 0.5
      memory   = "1Gi"
      env = {
        ReverseProxy__Clusters__identity__Destinations__d1__Address      = "http://ca-identity"
        ReverseProxy__Clusters__transactions__Destinations__d1__Address  = "http://ca-transactions"
        ReverseProxy__Clusters__consolidation__Destinations__d1__Address = "http://ca-consolidation"
        Edge__RequiredFrontDoorId                                        = var.front_door_id
        Cors__AllowedOrigins__0                                          = var.public_origin
      }
      secrets          = {}
      http_concurrency = 150
      queue_scaling    = false
    }
    web = {
      image            = "${local.registry}/cashflow/web:${var.image_tag}"
      external         = true
      min              = var.min_replicas
      max              = 3
      cpu              = 0.25
      memory           = "0.5Gi"
      env              = {}
      secrets          = {}
      http_concurrency = 200
      queue_scaling    = false
    }
  }
}

resource "azurerm_container_app" "service" {
  for_each                     = local.apps
  name                         = "ca-${each.key}"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"
  workload_profile_name        = "Consumption"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [var.identities[each.key].id]
  }

  registry {
    server   = local.registry
    identity = var.identities[each.key].id
  }

  # Segredos resolvidos do Key Vault pela identidade gerenciada (sem valores no estado do app).
  dynamic "secret" {
    for_each = toset(distinct(concat(values(each.value.secrets), each.value.queue_scaling ? ["rabbitmq-uri"] : [])))
    content {
      name                = secret.value
      identity            = var.identities[each.key].id
      key_vault_secret_id = var.secret_ids[secret.value]
    }
  }

  ingress {
    external_enabled = each.value.external
    target_port      = 8080
    transport        = "auto"
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = each.value.min
    max_replicas = each.value.max

    container {
      name   = each.key
      image  = each.value.image
      cpu    = each.value.cpu
      memory = each.value.memory

      dynamic "env" {
        for_each = each.key == "web" ? {} : merge(local.common_env, each.value.env)
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = each.value.secrets
        content {
          name        = env.key
          secret_name = env.value
        }
      }

      liveness_probe {
        transport = "HTTP"
        port      = 8080
        path      = each.key == "web" ? "/healthz" : "/health/live"
      }

      readiness_probe {
        transport = "HTTP"
        port      = 8080
        path      = each.key == "web" ? "/healthz" : "/health/ready"
      }
    }

    http_scale_rule {
      name                = "http-concurrency"
      concurrent_requests = tostring(each.value.http_concurrency)
    }

    # Consolidado escala pela profundidade da fila (KEDA): absorve picos sem afetar Lançamentos.
    dynamic "custom_scale_rule" {
      for_each = each.value.queue_scaling ? [1] : []
      content {
        name             = "rabbitmq-queue-depth"
        custom_rule_type = "rabbitmq"
        metadata = {
          queueName = "consolidation.transactions"
          mode      = "QueueLength"
          value     = "500"
        }
        authentication {
          secret_name       = "rabbitmq-uri"
          trigger_parameter = "host"
        }
      }
    }
  }

  depends_on = [azurerm_role_assignment.acr_pull]
}

# ---------- RabbitMQ (fase 1: nó único persistente; ver ADR-0003 para cluster em produção) ----------
resource "azurerm_container_app" "rabbitmq" {
  name                         = "ca-rabbitmq"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"
  workload_profile_name        = "Consumption"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [var.identities["rabbitmq"].id]
  }

  secret {
    name                = "rabbitmq-password"
    identity            = var.identities["rabbitmq"].id
    key_vault_secret_id = var.secret_ids["rabbitmq-password"]
  }

  ingress {
    external_enabled = false
    target_port      = 5672
    exposed_port     = 5672
    transport        = "tcp"
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = 1
    max_replicas = 1

    container {
      name   = "rabbitmq"
      image  = "docker.io/library/rabbitmq:4.1-management-alpine"
      cpu    = 1.0
      memory = "2Gi"

      env {
        name  = "RABBITMQ_DEFAULT_USER"
        value = "cashflow"
      }

      env {
        name        = "RABBITMQ_DEFAULT_PASS"
        secret_name = "rabbitmq-password"
      }

      volume_mounts {
        name = "data"
        path = "/var/lib/rabbitmq"
      }

      liveness_probe {
        transport = "TCP"
        port      = 5672
      }
    }

    volume {
      name         = "data"
      storage_name = azurerm_container_app_environment_storage.broker.name
      storage_type = "AzureFile"
    }
  }
}

# ---------- Job de bootstrap do banco: cria usuários por serviço (menor privilégio) ----------
resource "azurerm_container_app_job" "db_bootstrap" {
  name                         = "caj-db-bootstrap"
  location                     = var.location
  resource_group_name          = var.resource_group_name
  container_app_environment_id = azurerm_container_app_environment.this.id
  workload_profile_name        = "Consumption"
  replica_timeout_in_seconds   = 300
  replica_retry_limit          = 1
  tags                         = var.tags

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [var.identities["dbbootstrap"].id]
  }

  dynamic "secret" {
    for_each = toset(["pg-admin-password", "pg-transactions-password", "pg-consolidation-password", "pg-identity-password"])
    content {
      name                = secret.value
      identity            = var.identities["dbbootstrap"].id
      key_vault_secret_id = var.secret_ids[secret.value]
    }
  }

  template {
    container {
      name    = "bootstrap"
      image   = "docker.io/library/postgres:17-alpine"
      cpu     = 0.25
      memory  = "0.5Gi"
      command = ["/bin/sh", "-c"]
      args = [<<-SCRIPT
        set -e
        for svc in transactions consolidation identity; do
          var=$(echo "PW_$svc" | tr a-z A-Z); pw=$(eval echo \$$var)
          psql -v ON_ERROR_STOP=1 -v usr="svc_$svc" -v pass="$pw" -v db="cashflow_$svc" <<'SQL'
        SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'usr', :'pass') WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'usr') \gexec
        SELECT format('ALTER ROLE %I PASSWORD %L', :'usr', :'pass') \gexec
        SELECT format('ALTER DATABASE %I OWNER TO %I', :'db', :'usr') \gexec
        SELECT format('REVOKE ALL ON DATABASE %I FROM PUBLIC', :'db') \gexec
        SQL
        done
        SCRIPT
      ]

      env {
        name  = "PGHOST"
        value = var.postgres_fqdn
      }
      env {
        name  = "PGUSER"
        value = "cfadmin"
      }
      env {
        name  = "PGDATABASE"
        value = "postgres"
      }
      env {
        name  = "PGSSLMODE"
        value = "require"
      }
      env {
        name        = "PGPASSWORD"
        secret_name = "pg-admin-password"
      }
      env {
        name        = "PW_TRANSACTIONS"
        secret_name = "pg-transactions-password"
      }
      env {
        name        = "PW_CONSOLIDATION"
        secret_name = "pg-consolidation-password"
      }
      env {
        name        = "PW_IDENTITY"
        secret_name = "pg-identity-password"
      }
    }
  }
}
