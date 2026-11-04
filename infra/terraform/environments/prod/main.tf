# Produção: alta disponibilidade (zona redundante), mínimo de 2 réplicas por API, PITR 35 dias + geo-backup.
module "platform" {
  source = "../../modules/platform"

  environment = "prod"
  cost_center = var.cost_center
  owner       = var.owner
  alert_email = var.alert_email
  image_tag   = var.image_tag

  public_host_name = var.public_host_name
  min_replicas     = 2
  max_replicas     = 10
  zone_redundant   = true

  postgres_sku                   = "GP_Standard_D2ds_v5"
  postgres_storage_mb            = 131072
  postgres_backup_retention_days = 35
  postgres_geo_redundant_backup  = true
  postgres_high_availability     = true

  log_retention_days = 90
  log_daily_quota_gb = 10

  waf_mode              = "Prevention"
  allowed_countries     = ["BR"]
  key_vault_allowed_ips = var.key_vault_allowed_ips
  secrets_created_at    = var.secrets_created_at
  monthly_budget_usd    = 2600
  budget_start_date     = var.budget_start_date
}

output "public_endpoint" { value = module.platform.public_endpoint }
output "registry" { value = module.platform.registry }
