variable "name" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "subnet_id" { type = string }
variable "private_dns_zone_id" { type = string }
variable "admin_password" {
  type      = string
  sensitive = true
}
variable "sku_name" { type = string }
variable "storage_mb" {
  type    = number
  default = 32768
}
variable "storage_tier" {
  type    = string
  default = "P4"
}
variable "backup_retention_days" {
  type    = number
  default = 7
}
variable "geo_redundant_backup" {
  type    = bool
  default = false
}
variable "high_availability" {
  type    = bool
  default = false
}
variable "databases" { type = list(string) }
variable "action_group_id" { type = string }
variable "tags" { type = map(string) }
