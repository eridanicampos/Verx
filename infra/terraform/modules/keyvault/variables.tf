variable "name" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "services" { type = list(string) }
variable "purge_protection" {
  type    = bool
  default = true
}
variable "allowed_ip_ranges" {
  type    = list(string)
  default = []
}
variable "secrets_created_at" {
  description = "Data base (RFC3339) para cálculo da expiração dos segredos — força a política de rotação."
  type        = string
}
variable "secret_ttl" {
  type    = string
  default = "4320h" # 180 dias
}
variable "tags" { type = map(string) }
