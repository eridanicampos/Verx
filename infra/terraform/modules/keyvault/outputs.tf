output "key_vault_id" { value = azurerm_key_vault.this.id }
output "key_vault_uri" { value = azurerm_key_vault.this.vault_uri }
output "identities" {
  value = { for k, v in azurerm_user_assigned_identity.service : k => { id = v.id, principal_id = v.principal_id, client_id = v.client_id } }
}
output "secret_ids" {
  value = { for k, v in azurerm_key_vault_secret.generated : k => v.versionless_id }
}
output "pg_admin_password" {
  value     = random_password.generated["pg-admin-password"].result
  sensitive = true
}
output "service_passwords" {
  value = {
    transactions  = random_password.generated["pg-transactions-password"].result
    consolidation = random_password.generated["pg-consolidation-password"].result
    identity      = random_password.generated["pg-identity-password"].result
    rabbitmq      = random_password.generated["rabbitmq-password"].result
  }
  sensitive = true
}
