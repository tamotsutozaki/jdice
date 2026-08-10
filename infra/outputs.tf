output "url" {
  description = "Endereço público do sistema."
  value       = "https://${azurerm_container_app.web.ingress[0].fqdn}"
}

output "postgres_fqdn" {
  description = "Host do Postgres, para migrations/seed a partir do script up."
  value       = azurerm_postgresql_flexible_server.db.fqdn
}
