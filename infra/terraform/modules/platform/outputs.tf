output "resource_group" { value = azurerm_resource_group.this.name }
output "public_endpoint" { value = "https://${module.edge.endpoint_host_name}" }
output "registry" { value = module.container_apps.registry_login_server }
output "key_vault_uri" { value = module.keyvault.key_vault_uri }
output "postgres_fqdn" { value = module.postgres.fqdn }
