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
| `GET /api/auth/users` | **Admin** | lista contas |
| `DELETE /api/auth/users/{id}` | **Admin** | desativa conta |

No front, a gestão fica em `/usuarios` e o cadastro em `/usuarios/novo`, ambos
protegidos pelo `adminGuard`, e os links só aparecem para quem é administrador —
oferecê-los a quem seria barrado pelo guard seria um beco sem saída. O guard
evita a ida à rede, mas quem dá a palavra final é o servidor: as telas tratam o
`403` caso a sessão mude de perfil no meio do caminho.

### Desativação de contas

`DELETE` **não apaga a linha**: marca `DeactivatedAt`. A partir da Fase 4 cada
conta terá modelos e disparos associados, e remover o registro transformaria
"enviado por fulano" em "enviado por ninguém".

Três regras protegem a operação:

- ninguém desativa a própria conta, o que trancaria a pessoa para fora no meio
  do trabalho;
- o último administrador ativo não pode ser desativado — sobrando zero, a única
  saída seria alterar o banco na mão;
- quem é desativado **perde a sessão na hora**. Como o token vale 8h e carrega a
  role dentro dele, cada requisição autenticada confere se a conta ainda pode
  entrar. Isso custa uma consulta por chamada e abre mão de parte da vantagem de
  um token autocontido; é o preço de conseguir revogar acesso de verdade. Se o
  volume incomodar, o caminho é um cache curto das contas desativadas, não
  remover a verificação.

Conta desativada recebe no login a mesma resposta de senha errada: dizer "sua
conta foi desativada" confirmaria a existência do e-mail para quem está apenas
tentando adivinhar.

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

## Modelos de e-mail

| Rota | Acesso | O que faz |
|---|---|---|
| `GET /api/templates` | autenticado | lista, com busca e filtros |
| `GET /api/templates/{id}` | autenticado | detalhe com todas as versões |
| `POST /api/templates` | autenticado | cria o modelo e a versão 1 |
| `POST /api/templates/{id}/versions` | autenticado | cria a próxima versão |
| `PUT /api/templates/{id}` | autenticado | altera nome, categoria e tags |
| `POST /api/templates/preview` | autenticado | renderiza sem gravar |
| `DELETE /api/templates/{id}` | **Admin** | arquiva |

**O conteúdo de uma versão nunca é alterado — nem por Admin.** Não existe rota
para isso, e há teste que garante que continue não existindo. Mudar o texto é
criar a próxima versão; as anteriores ficam guardadas. É isso que permite
responder, meses depois, o que exatamente foi disparado.

Nome, categoria e tags **são** editáveis, e de propósito: não vão dentro do
e-mail, e congelá-los faria corrigir um erro de digitação numa tag gerar uma
versão de HTML idêntica à anterior — poluindo justamente o histórico que a
imutabilidade existe para proteger.

No projeto original isso era só convenção: a tela de edição deixava o número da
versão livre e o upload gravava com `REPLACE_EXISTING`, então editar mantendo o
mesmo número sobrescrevia o arquivo e o histórico sumia sem aviso.

### Variáveis

O motor é o **Scriban**, com sintaxe `{{ variavel }}`. As variáveis são
descobertas percorrendo a árvore sintática do template, não por expressão
regular — a diferença aparece nos casos reais:

```
{{ if vip }}Bem-vindo, {{ nome }}!{{ end }}
   → regex acha "nome" mas perde "vip", que é o que precisa ser preenchido

{{ for item in produtos }}{{ item }}{{ end }}
   → regex pediria "item", que só existe dentro da iteração
```

Conteúdo com erro de sintaxe é recusado ao salvar, já que a versão não poderá
ser corrigida depois. No preview, o mesmo erro volta junto da resposta em vez de
virar `400`: quem está escrevendo precisa ver o problema enquanto escreve.

## Destinatários

Funcionalidade **nova**: o projeto original não tinha destinatários. A tela de
envio lia `MOCK_LISTS` escrito no JSX, com "Lista Sul, 847 contatos" fixo no
código.

O destinatário existe uma vez só, identificado pelo e-mail, e participa de
quantas listas for preciso. Além de e-mail e nome, guarda **campos livres**
vindos das colunas extras da planilha — são eles que permitirão personalizar o
e-mail além do nome quando o disparo existir.

**Descadastrar vale para todas as listas.** Quem pede para não receber mais
espera parar de receber, e não parar só na lista de onde veio a mensagem que o
incomodou. Sair de uma lista é operação diferente, e não afeta as outras.

### Importação de CSV

A primeira linha é o cabeçalho e precisa de uma coluna `email`; `nome` é
opcional e qualquer outra coluna vira um campo do destinatário.

```
email;nome;empresa;plano
ana@empresa.com;Ana Souza;Acme;Premium
```

O leitor foi escrito à mão porque o que importa não é apenas separar campos, e
sim dizer, para cada linha recusada, **qual era o número dela e o motivo**.
Trata o que aparece em arquivo real: acentuação salva pelo Excel em
Windows-1252, o marcador de bytes que ele escreve e que grudaria no cabeçalho,
ponto e vírgula como separador, campos entre aspas contendo o separador, aspas
duplicadas e quebra de linha dentro de um campo.

Linhas válidas entram e as problemáticas voltam num relatório — recusar mil
linhas por causa de quatro erros faria a pessoa procurar o problema no escuro.
Quem já existe é atualizado, não duplicado, e os campos são **mesclados** sobre
os antigos: uma planilha só com `email;plano` não apaga a empresa cadastrada
antes.

## Estado

**Fase 3 — destinatários.** Cadastro, listas, descadastro e importação de CSV.
Envio entra na Fase 4 e agendamento na Fase 5.

