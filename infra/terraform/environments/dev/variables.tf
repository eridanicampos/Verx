variable "cost_center" { type = string }
variable "owner" { type = string }
variable "alert_email" { type = string }
variable "image_tag" { type = string }
variable "public_host_name" { type = string }
variable "secrets_created_at" { type = string }
variable "budget_start_date" { type = string }
variable "key_vault_allowed_ips" {
  type    = list(string)
  default = []
}
