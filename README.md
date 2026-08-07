# JDice

Sistema de gestão e disparo de e-mails corporativos: templates HTML, agendamento e controle de acesso. .NET, Angular, PostgreSQL, RabbitMQ e Docker.

Reescrita pessoal de um projeto acadêmico originalmente feito em Java/Spring Boot/React.

## Stack

| Camada | Tecnologia |
|---|---|
| API | ASP.NET Core (.NET 10) |
| Front | Angular 22 |
| Banco | PostgreSQL 17 |
| Templates | Scriban |
| Agendamento | Hangfire |
| Fila de disparo | RabbitMQ |
| E-mail | MailKit |
| Testes | xUnit, Testcontainers, Vitest |
| Infra | Docker Compose, GitHub Actions |

## Estrutura

```
src/
  Jdice.Api             HTTP, autenticação, composition root
  Jdice.Application     casos de uso e contratos
  Jdice.Domain          entidades e regras — não referencia nada
  Jdice.Infrastructure  EF Core, MailKit, Hangfire, RabbitMQ
tests/
  Jdice.UnitTests
  Jdice.IntegrationTests
web/                    Angular
```

A direção das dependências entre camadas é verificada por teste
(`ArquiteturaDeCamadasTests`): se alguém instalar EF Core no `Domain`, o CI quebra.

## Como rodar

### Tudo em containers

```bash
docker compose up --build
```

- Front: http://localhost:8080
- API: http://localhost:5080
- Health: http://localhost:5080/health/ready

Não é preciso criar `.env` — todas as variáveis têm default no compose.
Para customizar, copie `.env.example` para `.env`.

### Desenvolvimento local

O banco em container, API e front na máquina:

```bash
docker compose up -d postgres

dotnet run --project src/Jdice.Api        # http://localhost:5080
cd web && npm start                        # http://localhost:4200
```

O `ng serve` faz proxy de `/api` e `/health` para a API (`web/proxy.conf.json`),
então o front sempre usa caminho relativo e não existe URL de backend no bundle.

## Testes

```bash
dotnet test           # backend
cd web && npm test    # frontend
```

## Health checks

| Endpoint | O que verifica | Uso |
|---|---|---|
| `/health/live` | o processo está de pé | liveness, teste de integração |
| `/health/ready` | consegue falar com o Postgres | readiness, healthcheck do compose |

A separação existe para que uma indisponibilidade momentânea do banco não
derrube a API, e para o teste de integração poder subir sem infraestrutura.

## Estado

**Fase 0 — esqueleto.** Solução, health checks, front mínimo, compose e CI.
Sem domínio ainda: autenticação entra na Fase 1, templates na Fase 2,
destinatários na Fase 3, envio na Fase 4, agendamento na Fase 5.

