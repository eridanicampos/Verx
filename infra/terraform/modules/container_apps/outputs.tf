output "gateway_fqdn" { value = azurerm_container_app.service["gateway"].ingress[0].fqdn }
output "web_fqdn" { value = azurerm_container_app.service["web"].ingress[0].fqdn }
output "registry_login_server" { value = azurerm_container_registry.this.login_server }
output "environment_id" { value = azurerm_container_app_environment.this.id }
