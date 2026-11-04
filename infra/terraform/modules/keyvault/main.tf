# Gestão de segredos: nenhum segredo em texto puro em código ou variáveis de ambiente.
# Cada serviço tem uma identidade gerenciada própria com acesso SOMENTE de leitura a segredos.
data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "this" {
  name                          = "kv-${var.name}"
  location                      = var.location
  resource_group_name           = var.resource_group_name
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  sku_name                      = "standard"
  rbac_authorization_enabled    = true
  purge_protection_enabled      = var.purge_protection
  soft_delete_retention_days    = 90
  public_network_access_enabled = true # restringido por network_acls; ver docs/seguranca (private endpoint em evolução)
  tags                          = var.tags

  network_acls {
    bypass         = "AzureServices"
    default_action = var.allowed_ip_ranges == [] ? "Allow" : "Deny"
    ip_rules       = var.allowed_ip_ranges
  }
}

resource "azurerm_role_assignment" "deployer" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_user_assigned_identity" "service" {
  for_each            = toset(var.services)
  name                = "id-${var.name}-${each.key}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_role_assignment" "secrets_user" {
  for_each             = azurerm_user_assigned_identity.service
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = each.value.principal_id
}

# ---------- Segredos gerados (rotacionáveis via taint/replace) ----------
resource "random_password" "generated" {
  for_each         = toset(["pg-admin-password", "pg-transactions-password", "pg-consolidation-password", "pg-identity-password", "rabbitmq-password", "seed-operator-password", "seed-analyst-password"])
  length           = 32
  special          = true
  override_special = "-_.~"
}

resource "random_bytes" "aes_keys" {
  for_each = toset(["pii-encryption-key-k1", "pii-blind-index-key"])
  length   = 32
}

# Chave de assinatura dos JWT (RS256). Alternativa futura: assinatura via Key Vault Keys/HSM
# sem exportar a chave privada (ver ADR-0005).
resource "tls_private_key" "jwt" {
  algorithm = "RSA"
  rsa_bits  = 3072
}

locals {
  secrets = merge(
    { for k, v in random_password.generated : k => v.result },
    { for k, v in random_bytes.aes_keys : k => v.base64 },
    { "jwt-signing-key" = tls_private_key.jwt.private_key_pem_pkcs8 },
  )
}

resource "azurerm_key_vault_secret" "generated" {
  for_each        = nonsensitive(toset(keys(local.secrets)))
  name            = each.key
  value           = local.secrets[each.key]
  key_vault_id    = azurerm_key_vault.this.id
  content_type    = "generated-by-terraform"
  expiration_date = timeadd(var.secrets_created_at, var.secret_ttl)
  tags            = var.tags

  depends_on = [azurerm_role_assignment.deployer]

  lifecycle {
    ignore_changes = [expiration_date]
  }
}
