# Desenvolvimento: escala a zero, sem HA e sem geo-backup — custo mínimo (ver docs/finops).
module "platform" {
  source = "../../modules/platform"

  environment   = "dev"
  address_space = "10.41.0.0/16"
  cost_center   = var.cost_center
  owner         = var.owner
  alert_email   = var.alert_email
  image_tag     = var.image_tag

  public_host_name = var.public_host_name
  min_replicas     = 0
  max_replicas     = 3
  zone_redundant   = false

  postgres_sku                   = "B_Standard_B1ms"
  postgres_storage_mb            = 32768
  postgres_backup_retention_days = 7
  postgres_geo_redundant_backup  = false
  postgres_high_availability     = false

  log_retention_days = 30
  log_daily_quota_gb = 2

  waf_mode           = "Detection"
  secrets_created_at = var.secrets_created_at
  monthly_budget_usd = 700
  budget_start_date  = var.budget_start_date
}

output "public_endpoint" { value = module.platform.public_endpoint }
