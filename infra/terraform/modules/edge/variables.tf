variable "name" { type = string }
variable "resource_group_name" { type = string }
variable "profile_id" { type = string }
variable "gateway_fqdn" { type = string }
variable "web_fqdn" { type = string }
variable "log_analytics_workspace_id" { type = string }
variable "waf_mode" {
  type    = string
  default = "Prevention"
  validation {
    condition     = contains(["Prevention", "Detection"], var.waf_mode)
    error_message = "waf_mode deve ser Prevention ou Detection."
  }
}
variable "rate_limit_per_minute" {
  type    = number
  default = 3000
}
variable "allowed_countries" {
  description = "Códigos ISO de países permitidos (vazio = sem filtro geográfico)."
  type        = list(string)
  default     = []
}
variable "tags" { type = map(string) }
