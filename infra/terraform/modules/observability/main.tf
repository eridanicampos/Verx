resource "azurerm_log_analytics_workspace" "this" {
  name                = "log-${var.name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = var.retention_days
  daily_quota_gb      = var.daily_quota_gb # FinOps: teto de ingestão diária
  tags                = var.tags
}

resource "azurerm_application_insights" "this" {
  name                = "appi-${var.name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  workspace_id        = azurerm_log_analytics_workspace.this.id
  application_type    = "web"
  sampling_percentage = var.sampling_percentage
  tags                = var.tags
}

resource "azurerm_monitor_action_group" "oncall" {
  name                = "ag-${var.name}-oncall"
  resource_group_name = var.resource_group_name
  short_name          = "cf-oncall"
  tags                = var.tags

  email_receiver {
    name                    = "oncall"
    email_address           = var.alert_email
    use_common_alert_schema = true
  }
}
