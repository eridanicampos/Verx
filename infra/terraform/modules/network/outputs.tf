output "apps_subnet_id" { value = azurerm_subnet.apps.id }
output "database_subnet_id" { value = azurerm_subnet.database.id }
output "postgres_private_dns_zone_id" { value = azurerm_private_dns_zone.postgres.id }
output "vnet_id" { value = azurerm_virtual_network.this.id }
