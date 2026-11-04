# PostgreSQL Flexible Server: acesso apenas privado (VNet), TLS obrigatório, backups com PITR,
# HA com zona redundante em produção. Database-per-service no mesmo servidor (isolamento lógico;
# separar servidores é a evolução natural quando o perfil de carga divergir — ver ADR-0002).
resource "azurerm_postgresql_flexible_server" "this" {
  name                          = "psql-${var.name}"
  location                      = var.location
  resource_group_name           = var.resource_group_name
  version                       = "16"
  sku_name                      = var.sku_name
  storage_mb                    = var.storage_mb
  storage_tier                  = var.storage_tier
  auto_grow_enabled             = true
  backup_retention_days         = var.backup_retention_days
  geo_redundant_backup_enabled  = var.geo_redundant_backup
  delegated_subnet_id           = var.subnet_id
  private_dns_zone_id           = var.private_dns_zone_id
  public_network_access_enabled = false
  administrator_login           = "cfadmin"
  administrator_password        = var.admin_password
  zone                          = "1"
  tags                          = var.tags

  dynamic "high_availability" {
    for_each = var.high_availability ? [1] : []
    content {
      mode                      = "ZoneRedundant"
      standby_availability_zone = "2"
    }
  }

  maintenance_window {
    day_of_week  = 0
    start_hour   = 3
    start_minute = 0
  }

  lifecycle {
    ignore_changes = [zone, high_availability[0].standby_availability_zone]
  }
}

resource "azurerm_postgresql_flexible_server_configuration" "settings" {
  for_each = {
    "require_secure_transport"   = "on"
    "log_min_duration_statement" = "500"
    "log_connections"            = "on"
  }
  name      = each.key
  server_id = azurerm_postgresql_flexible_server.this.id
  value     = each.value
}

resource "azurerm_postgresql_flexible_server_database" "service" {
  for_each  = toset(var.databases)
  name      = each.key
  server_id = azurerm_postgresql_flexible_server.this.id
  charset   = "UTF8"
  collation = "en_US.utf8"

  lifecycle {
    prevent_destroy = false # true em produção via política; mantido false para permitir destroy do ambiente de avaliação
  }
}

resource "azurerm_monitor_metric_alert" "cpu" {
  name                = "alert-${var.name}-psql-cpu"
  resource_group_name = var.resource_group_name
  scopes              = [azurerm_postgresql_flexible_server.this.id]
  description         = "CPU do PostgreSQL acima de 80% por 15 minutos"
  severity            = 2
  frequency           = "PT5M"
  window_size         = "PT15M"
  tags                = var.tags

  criteria {
    metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
    metric_name      = "cpu_percent"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 80
  }

  action {
    action_group_id = var.action_group_id
  }
}
