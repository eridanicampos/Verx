variable "name" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "address_space" {
  type    = string
  default = "10.40.0.0/16"
}
variable "tags" { type = map(string) }
