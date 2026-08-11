output "url" {
  description = "Endereço público do sistema."
  value       = "https://${azurerm_container_app.web.ingress[0].fqdn}"
}
