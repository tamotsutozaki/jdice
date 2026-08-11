output "url" {
  description = "Endereço público do sistema."
  value       = "https://${azurerm_container_app.web.ingress[0].fqdn}"
}

output "mailpit_url" {
  description = "Interface web do Mailpit — ver os e-mails capturados na demo."
  value       = "https://${azapi_resource.mailpit.output.properties.configuration.ingress.fqdn}"
}
