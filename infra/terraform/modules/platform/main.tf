# Composição da plataforma CashFlow. Ambientes (dev/prod) só variam dimensionamento e políticas.
locals {
  name = "${var.project}-${var.environment}"
  tags = merge(var.tags, {
    project     = var.project
    environment = var.environment
    managed-by  = "terraform"
    cost-center = var.cost_center
    owner       = var.owner
  })
  services = ["identity", "transactions", "consolidation", "gateway", "web", "rabbitmq", "dbbootstrap"]
}

resource "azurerm_resource_group" "this" {
  name     = "rg-${local.name}"
  location = var.location
  tags     = local.tags
}

module "network" {
  source              = "../network"
  name                = local.name
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
  address_space       = var.address_space
  tags                = local.tags
}

module "observability" {
  source              = "../observability"
  name                = local.name
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
  retention_days      = var.log_retention_days
  daily_quota_gb      = var.log_daily_quota_gb
  alert_email         = var.alert_email
  tags                = local.tags
}

module "keyvault" {
  source              = "../keyvault"
  name                = local.name
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
  services            = local.services
  purge_protection    = var.environment == "prod"
  allowed_ip_ranges   = var.key_vault_allowed_ips
  secrets_created_at  = var.secrets_created_at
  tags                = local.tags
}

module "postgres" {
  source                = "../postgres"
  name                  = local.name
  location              = var.location
  resource_group_name   = azurerm_resource_group.this.name
  subnet_id             = module.network.database_subnet_id
  private_dns_zone_id   = module.network.postgres_private_dns_zone_id
  admin_password        = module.keyvault.pg_admin_password
  sku_name              = var.postgres_sku
  storage_mb            = var.postgres_storage_mb
  backup_retention_days = var.postgres_backup_retention_days
  geo_redundant_backup  = var.postgres_geo_redundant_backup
  high_availability     = var.postgres_high_availability
  databases             = ["cashflow_transactions", "cashflow_consolidation", "cashflow_identity"]
  action_group_id       = module.observability.action_group_id
  tags                  = local.tags

  depends_on = [module.network]
}

# Segredos que dependem do servidor (connection strings) — gravados no Key Vault, nunca em texto puro nos apps.
locals {
  service_passwords = module.keyvault.service_passwords
  derived_secrets = {
    "cs-transactions"  = "Host=${module.postgres.fqdn};Port=5432;Database=cashflow_transactions;Username=svc_transactions;Password=${local.service_passwords.transactions};SSL Mode=Require;Maximum Pool Size=60"
    "cs-consolidation" = "Host=${module.postgres.fqdn};Port=5432;Database=cashflow_consolidation;Username=svc_consolidation;Password=${local.service_passwords.consolidation};SSL Mode=Require;Maximum Pool Size=60"
    "cs-identity"      = "Host=${module.postgres.fqdn};Port=5432;Database=cashflow_identity;Username=svc_identity;Password=${local.service_passwords.identity};SSL Mode=Require"
    "rabbitmq-uri"     = "amqp://cashflow:${local.service_passwords.rabbitmq}@ca-rabbitmq:5672/"
  }
}

resource "azurerm_key_vault_secret" "derived" {
  for_each     = nonsensitive(toset(keys(local.derived_secrets)))
  name         = each.key
  value        = local.derived_secrets[each.key]
  key_vault_id = module.keyvault.key_vault_id
  content_type = "connection-string"
  tags         = local.tags
}

resource "azurerm_cdn_frontdoor_profile" "this" {
  name                     = "afd-${local.name}"
  resource_group_name      = azurerm_resource_group.this.name
  sku_name                 = "Premium_AzureFrontDoor"
  response_timeout_seconds = 30
  tags                     = local.tags
}

module "container_apps" {
  source                     = "../container_apps"
  name                       = local.name
  location                   = var.location
  resource_group_name        = azurerm_resource_group.this.name
  subnet_id                  = module.network.apps_subnet_id
  log_analytics_workspace_id = module.observability.log_analytics_workspace_id
  identities                 = module.keyvault.identities
  secret_ids                 = merge(module.keyvault.secret_ids, { for k, v in azurerm_key_vault_secret.derived : k => v.versionless_id })
  postgres_fqdn              = module.postgres.fqdn
  image_tag                  = var.image_tag
  min_replicas               = var.min_replicas
  max_replicas               = var.max_replicas
  zone_redundant             = var.zone_redundant
  front_door_id              = azurerm_cdn_frontdoor_profile.this.resource_guid
  public_origin              = "https://${var.public_host_name}"
  tags                       = local.tags
}

module "edge" {
  source                     = "../edge"
  name                       = local.name
  resource_group_name        = azurerm_resource_group.this.name
  profile_id                 = azurerm_cdn_frontdoor_profile.this.id
  gateway_fqdn               = module.container_apps.gateway_fqdn
  web_fqdn                   = module.container_apps.web_fqdn
  log_analytics_workspace_id = module.observability.log_analytics_workspace_id
  waf_mode                   = var.waf_mode
  allowed_countries          = var.allowed_countries
  tags                       = local.tags
}

# FinOps: orçamento mensal do resource group com alertas de 80% (real) e 100% (previsto).
resource "azurerm_consumption_budget_resource_group" "this" {
  name              = "budget-${local.name}"
  resource_group_id = azurerm_resource_group.this.id
  amount            = var.monthly_budget_usd
  time_grain        = "Monthly"

  time_period {
    start_date = var.budget_start_date
  }

  notification {
    enabled        = true
    threshold      = 80
    operator       = "GreaterThan"
    threshold_type = "Actual"
    contact_emails = [var.alert_email]
  }

  notification {
    enabled        = true
    threshold      = 100
    operator       = "GreaterThan"
    threshold_type = "Forecasted"
    contact_emails = [var.alert_email]
  }
}
