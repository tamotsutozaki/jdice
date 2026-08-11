locals {
  # Postgres roda como container interno; conexão simples, sem TLS (a rede do
  # Container Apps Environment é privada).
  pg_connection = "Host=${azurerm_container_app.postgres.ingress[0].fqdn};Port=5432;Database=jdice;Username=jdice;Password=${var.postgres_password}"

  # FQDNs internos: os apps se acham dentro do mesmo Container Apps Environment.
  rabbit_host = azurerm_container_app.rabbitmq.ingress[0].fqdn

  # O Mailpit expõe a UI na porta principal (externa) e o SMTP como porta
  # adicional interna. Portas adicionais são alcançadas pelo NOME do app, não
  # pelo FQDN do ingress — por isso aqui é só "mailpit", com a 1025 no Smtp:Port.
  mailpit_host = "mailpit"

  image = {
    api    = "ghcr.io/${var.ghcr_owner}/jdice-api:${var.image_tag}"
    worker = "ghcr.io/${var.ghcr_owner}/jdice-worker:${var.image_tag}"
    web    = "ghcr.io/${var.ghcr_owner}/jdice-web:${var.image_tag}"
  }
}

# ── Postgres: banco em container, interno. Efêmero de propósito — o banco é
# recriado com seed a cada `up`, então não há volume persistente. Substitui o
# servidor gerenciado para o custo caber na cota grátis do Container Apps. ──
resource "azurerm_container_app" "postgres" {
  name                         = "postgres"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"

  secret {
    name  = "pg-pass"
    value = var.postgres_password
  }

  template {
    min_replicas = 1
    max_replicas = 1
    container {
      name   = "postgres"
      image  = "postgres:17-alpine"
      cpu    = 0.5
      memory = "1Gi"
      env {
        name  = "POSTGRES_DB"
        value = "jdice"
      }
      env {
        name  = "POSTGRES_USER"
        value = "jdice"
      }
      env {
        name        = "POSTGRES_PASSWORD"
        secret_name = "pg-pass"
      }
    }
  }

  ingress {
    external_enabled = false
    transport        = "tcp"
    target_port      = 5432
    exposed_port     = 5432
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

# ── Mailpit: SMTP de captura + interface web. Precisa de DUAS portas (UI 8025 e
# SMTP 1025), o que o azurerm não suporta — por isso vai via AzAPI, que fala
# direto com a API da Azure. A UI é a porta principal, externa (para ver os
# e-mails capturados); o SMTP é porta adicional interna (a api/worker entregam
# em mailpit:1025). Temporário até o ACS entrar no lugar. ──
resource "azapi_resource" "mailpit" {
  type      = "Microsoft.App/containerApps@2025-01-01"
  name      = "mailpit"
  location  = azurerm_resource_group.rg.location
  parent_id = azurerm_resource_group.rg.id

  body = {
    properties = {
      managedEnvironmentId = azurerm_container_app_environment.env.id
      configuration = {
        activeRevisionsMode = "Single"
        ingress = {
          external   = true
          targetPort = 8025
          transport  = "auto"
          traffic = [{
            latestRevision = true
            weight         = 100
          }]
          additionalPortMappings = [{
            external   = false
            targetPort = 1025
          }]
        }
      }
      template = {
        containers = [{
          name  = "mailpit"
          image = "axllent/mailpit:v1.21"
          resources = {
            cpu    = 0.25
            memory = "0.5Gi"
          }
        }]
        scale = {
          minReplicas = 1
          maxReplicas = 1
        }
      }
    }
  }

  response_export_values = ["properties.configuration.ingress.fqdn"]
}

# ── RabbitMQ: fila de fan-out, interna por TCP. ──
resource "azurerm_container_app" "rabbitmq" {
  name                         = "rabbitmq"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"

  secret {
    name  = "rabbit-pass"
    value = var.rabbitmq_password
  }

  template {
    min_replicas = 1
    max_replicas = 1
    container {
      name   = "rabbitmq"
      image  = "rabbitmq:4-management-alpine"
      cpu    = 0.5
      memory = "1Gi"
      env {
        name  = "RABBITMQ_DEFAULT_USER"
        value = "jdice"
      }
      env {
        name        = "RABBITMQ_DEFAULT_PASS"
        secret_name = "rabbit-pass"
      }
    }
  }

  ingress {
    external_enabled = false
    transport        = "tcp"
    target_port      = 5672
    exposed_port     = 5672
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

# ── API: interna. Só o front público a alcança, via proxy do nginx. ──
resource "azurerm_container_app" "api" {
  name                         = "api"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"

  secret {
    name  = "db-connection"
    value = local.pg_connection
  }
  secret {
    name  = "jwt-key"
    value = var.jwt_signing_key
  }
  secret {
    name  = "seed-password"
    value = var.seed_admin_password
  }
  secret {
    name  = "rabbit-pass"
    value = var.rabbitmq_password
  }

  template {
    min_replicas = 1
    max_replicas = 1
    container {
      name   = "api"
      image  = local.image.api
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }
      env {
        name        = "ConnectionStrings__Postgres"
        secret_name = "db-connection"
      }
      env {
        name        = "Jwt__SigningKey"
        secret_name = "jwt-key"
      }
      env {
        name  = "Seed__AdminEmail"
        value = var.seed_admin_email
      }
      env {
        name        = "Seed__AdminPassword"
        secret_name = "seed-password"
      }
      env {
        name  = "Email__Provider"
        value = "Mailpit"
      }
      env {
        name  = "Smtp__Host"
        value = local.mailpit_host
      }
      env {
        name  = "Smtp__Port"
        value = "1025"
      }
      env {
        name  = "Smtp__FromEmail"
        value = "nao-responda@jdice.local"
      }
      env {
        name  = "Smtp__FromName"
        value = "JDice"
      }
      env {
        name  = "RabbitMq__Enabled"
        value = "true"
      }
      env {
        name  = "RabbitMq__Host"
        value = local.rabbit_host
      }
      env {
        name  = "RabbitMq__Username"
        value = "jdice"
      }
      env {
        name        = "RabbitMq__Password"
        secret_name = "rabbit-pass"
      }
    }
  }

  ingress {
    external_enabled = false
    transport        = "http"
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

# ── Worker: processa os disparos. Sem ingress, sempre de pé (min 1). ──
resource "azurerm_container_app" "worker" {
  name                         = "worker"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"

  secret {
    name  = "db-connection"
    value = local.pg_connection
  }
  secret {
    name  = "jwt-key"
    value = var.jwt_signing_key
  }
  secret {
    name  = "rabbit-pass"
    value = var.rabbitmq_password
  }

  template {
    min_replicas = 1
    max_replicas = 1
    container {
      name   = "worker"
      image  = local.image.worker
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "DOTNET_ENVIRONMENT"
        value = "Production"
      }
      env {
        name        = "ConnectionStrings__Postgres"
        secret_name = "db-connection"
      }
      env {
        name        = "Jwt__SigningKey"
        secret_name = "jwt-key"
      }
      env {
        name  = "Email__Provider"
        value = "Mailpit"
      }
      env {
        name  = "Smtp__Host"
        value = local.mailpit_host
      }
      env {
        name  = "Smtp__Port"
        value = "1025"
      }
      env {
        name  = "Smtp__FromEmail"
        value = "nao-responda@jdice.local"
      }
      env {
        name  = "Smtp__FromName"
        value = "JDice"
      }
      env {
        name  = "Hangfire__WorkerCount"
        value = "4"
      }
      env {
        name  = "RabbitMq__Enabled"
        value = "true"
      }
      env {
        name  = "RabbitMq__Host"
        value = local.rabbit_host
      }
      env {
        name  = "RabbitMq__Username"
        value = "jdice"
      }
      env {
        name        = "RabbitMq__Password"
        secret_name = "rabbit-pass"
      }
    }
  }
}

# ── Web: nginx público. Faz proxy de /api para o FQDN interno da API. ──
resource "azurerm_container_app" "web" {
  name                         = "web"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"

  template {
    min_replicas = 1
    max_replicas = 2
    container {
      name   = "web"
      image  = local.image.web
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "API_UPSTREAM"
        value = "http://${azurerm_container_app.api.ingress[0].fqdn}"
      }
      env {
        name  = "NGINX_ENVSUBST_FILTER"
        value = "API_UPSTREAM"
      }
    }
  }

  ingress {
    external_enabled = true
    transport        = "auto"
    target_port      = 80
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}
