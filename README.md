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
cp .env.example .env
# gere a chave e cole em JWT_SIGNING_KEY, e defina SEED_ADMIN_PASSWORD
openssl rand -base64 48

docker compose up --build
```

- Front: http://localhost:8080
- API: http://localhost:5080
- Health: http://localhost:5080/health/ready

O `.env` é obrigatório: `JWT_SIGNING_KEY` não tem valor padrão e o compose
recusa subir sem ela. É proposital — um segredo com default acaba virando o
segredo de produção de alguém. As demais variáveis têm default.

O administrador inicial é criado a partir de `SEED_ADMIN_EMAIL` e
`SEED_ADMIN_PASSWORD`, e apenas quando o banco ainda não tem nenhuma conta.
Sem eles a aplicação sobe, avisa no log e fica sem forma de entrar — não
existe usuário padrão embutido no código.

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

## Autenticação

| Rota | Acesso | O que faz |
|---|---|---|
| `POST /api/auth/login` | pública | valida credencial e grava o cookie de sessão |
| `POST /api/auth/logout` | pública | apaga o cookie |
| `GET /api/auth/me` | autenticado | quem está logado |
| `POST /api/auth/users` | **Admin** | cria conta |

No front, o cadastro fica em `/usuarios/novo`, protegido pelo `adminGuard`, e o
link para ele só aparece para quem é administrador — oferecê-lo a quem seria
barrado pelo guard seria um beco sem saída. O guard evita a ida à rede, mas quem
dá a palavra final é o servidor: a tela trata o `403` caso a sessão mude de
perfil no meio do caminho.

O token é um JWT em **cookie httpOnly**, não em `localStorage`: o JavaScript não
consegue lê-lo, então um XSS não rouba a sessão. Como o front e a API são
servidos pela mesma origem, o cookie usa `SameSite=Strict` e não há CSRF a
tratar. A role viaja dentro do token, o que dispensa consultar o banco a cada
requisição.

Diferenças deliberadas em relação ao projeto original, onde cada uma destas era
um problema real:

- criar conta é restrito a Admin — antes o endpoint era público e ainda aceitava
  a role no corpo, então qualquer pessoa criava um administrador;
- a expiração do token é calculada em UTC — antes somava horas no fuso da
  máquina e carimbava offset `-03:00` fixo, o que dava validade errada fora de
  Brasília;
- e-mail e senha errados devolvem a mesma resposta, e o login gasta o mesmo
  tempo nos dois casos, para não revelar quem tem conta;
- o login é limitado por IP (10 tentativas por minuto, configurável em
  `RateLimiting:Login`), porque o BCrypt é lento de propósito e tentativas sem
  limite viram tanto força bruta quanto exaustão de CPU.

A política de senha (12 a 128 caracteres) vive em `PasswordPolicy`, no domínio,
e é aplicada tanto no contrato da API quanto no serviço — o seed cria contas
pelo serviço, sem passar pelo contrato, e uma regra que só existisse no DTO não
valeria para ele.

Cadastros simultâneos do mesmo e-mail resultam em uma conta e conflitos: a
checagem prévia serve para responder rápido, mas quem garante a unicidade é o
índice do banco, cuja violação é traduzida em `409`.

## Estado

**Fase 1 — autenticação.** Contas, login, perfis e seed do administrador.
Templates entram na Fase 2, destinatários na Fase 3, envio na Fase 4,
agendamento na Fase 5.

