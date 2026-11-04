variable "name" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "subnet_id" { type = string }
variable "log_analytics_workspace_id" { type = string }
variable "identities" {
  type = map(object({ id = string, principal_id = string, client_id = string }))
}
variable "secret_ids" {
  description = "Mapa nome-lógico => versionless id do segredo no Key Vault."
  type        = map(string)
}
variable "postgres_fqdn" { type = string }
variable "image_tag" { type = string }
variable "min_replicas" { type = number }
variable "max_replicas" { type = number }
variable "zone_redundant" {
  type    = bool
  default = false
}
variable "acr_sku" {
  type    = string
  default = "Standard"
}
variable "front_door_id" {
  description = "Resource GUID do perfil do Front Door; o Gateway rejeita tráfego sem o header X-Azure-FDID correspondente."
  type        = string
}
variable "public_origin" { type = string }
variable "tags" { type = map(string) }
