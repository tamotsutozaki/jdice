resource "azurerm_postgresql_flexible_server" "db" {
  name                = "${var.prefix}-pg-${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location

  version                       = "16"
  administrator_login           = "jdice"
  administrator_password        = var.postgres_password
  public_network_access_enabled = true

  # Menor tier burstable — suficiente para portfólio e barato.
  sku_name   = "B_Standard_B1ms"
  storage_mb = 32768

  # Banco descartável: é recriado com seed a cada `up`, então não faz sentido
  # pagar backup de longa duração.
  backup_retention_days = 7
  zone                  = "1"

  authentication {
    password_auth_enabled = true
  }

  # A senha muda a cada `up` (segredo gerado no script); ignorar aqui evita que
  # o Terraform tente "corrigir" fora de hora.
  lifecycle {
    ignore_changes = [zone]
  }
}

resource "azurerm_postgresql_flexible_server_database" "jdice" {
  name      = "jdice"
  server_id = azurerm_postgresql_flexible_server.db.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# Libera o acesso de qualquer IP. É um banco descartável, recriado a cada demo e
# ainda protegido por senha; abrir assim evita depurar faixa de IP de saída do
# Container Apps num ambiente que existe por minutos.
resource "azurerm_postgresql_flexible_server_firewall_rule" "allow_all" {
  name             = "allow-all"
  server_id        = azurerm_postgresql_flexible_server.db.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "255.255.255.255"
}
