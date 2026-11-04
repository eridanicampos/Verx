variable "name" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "retention_days" {
  type    = number
  default = 30
}
variable "daily_quota_gb" {
  type    = number
  default = 5
}
variable "sampling_percentage" {
  type    = number
  default = 25
}
variable "alert_email" { type = string }
variable "tags" { type = map(string) }
