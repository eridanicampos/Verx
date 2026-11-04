variable "project" {
  type    = string
  default = "cashflow"
}
variable "environment" {
  type = string
  validation {
    condition     = contains(["dev", "hml", "prod"], var.environment)
    error_message = "environment deve ser dev, hml ou prod."
  }
}
variable "location" {
  type    = string
  default = "brazilsouth" # residência de dados no Brasil (LGPD / Res. BCB 4.893)
}
variable "address_space" {
  type    = string
  default = "10.40.0.0/16"
}
variable "cost_center" { type = string }
variable "owner" { type = string }
variable "alert_email" { type = string }
variable "image_tag" { type = string }
variable "public_host_name" {
  description = "Host público (domínio customizado ou endpoint do Front Door)."
  type        = string
}
variable "min_replicas" { type = number }
variable "max_replicas" { type = number }
variable "zone_redundant" { type = bool }
variable "postgres_sku" { type = string }
variable "postgres_storage_mb" { type = number }
variable "postgres_backup_retention_days" { type = number }
variable "postgres_geo_redundant_backup" { type = bool }
variable "postgres_high_availability" { type = bool }
variable "log_retention_days" { type = number }
variable "log_daily_quota_gb" { type = number }
variable "waf_mode" {
  type    = string
  default = "Prevention"
}
variable "allowed_countries" {
  type    = list(string)
  default = []
}
variable "key_vault_allowed_ips" {
  type    = list(string)
  default = []
}
variable "secrets_created_at" { type = string }
variable "monthly_budget_usd" { type = number }
variable "budget_start_date" {
  description = "Primeiro dia do mês (RFC3339), ex.: 2026-11-01T00:00:00Z"
  type        = string
}
variable "tags" {
  type    = map(string)
  default = {}
}
