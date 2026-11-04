terraform {
  required_version = ">= 1.9.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.12.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.9.1"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.4.2"
    }
  }

  # Estado remoto criptografado, com lock (blob lease) e acesso via Entra ID (sem access key).
  backend "azurerm" {
    resource_group_name  = "rg-cashflow-tfstate"
    storage_account_name = "stcashflowtfstate"
    container_name       = "tfstate"
    key                  = "cashflow-prod.tfstate"
    use_azuread_auth     = true
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = false
    }
    resource_group {
      prevent_deletion_if_contains_resources = true
    }
  }
  storage_use_azuread = true
}
