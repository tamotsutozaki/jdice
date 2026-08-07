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

## Disparos

| Rota | Acesso | O que faz |
|---|---|---|
| `GET /api/campaigns` | autenticado | lista, com filtro por situação |
| `GET /api/campaigns/{id}` | autenticado | detalhe com o resumo das entregas |
| `GET /api/campaigns/{id}/deliveries` | autenticado | situação por destinatário |
| `POST /api/campaigns` | autenticado | cria e envia, ou agenda |
| `POST /api/campaigns/{id}/cancel` | autenticado | cancela o que ainda não saiu |
| `POST /api/campaigns/{id}/reschedule` | autenticado | muda data e fuso |

Criar um disparo devolve **202**, não 200: o trabalho foi aceito, não concluído.
Segurar a conexão até o fim do envio faria a tela expirar num disparo grande.

O disparo congela quem vai receber no momento da criação — uma linha de entrega
por destinatário. Sem isso, alguém adicionado à lista no meio do processamento
receberia ou não dependendo da velocidade do worker.

**Cada entrega é uma linha com situação própria** (`Pending`, `Sending`, `Sent`,
`Failed`, `Skipped`), tentativas e o motivo da última falha. É a diferença entre
"o disparo falhou" e "estes três e-mails falharam, por este motivo".

Duas verificações acontecem no momento do envio, não no da criação:

- **descadastro**, porque entre criar e enviar pode passar um dia, e a decisão
  da pessoa vale até o último instante — quem saiu vira `Skipped`, não some;
- **cancelamento**, para que um job já enfileirado não envie assim mesmo.

### Não enviar duas vezes

Redelivery de fila, retry do Hangfire e worker reiniciado no meio levam todos ao
mesmo lugar: processar de novo. A entrega resolve isso na transição de estado —
`TryStart()` só sai de `Pending`, e quem chega depois encontra `Sending` ou
`Sent` e desiste. O `UPDATE` condicional acontece no banco, então dois workers
disputando a mesma entrega produzem um envio, não dois.

Falha temporária volta para `Pending` e é tentada de novo, até três vezes; a
partir daí vira `Failed` com o motivo registrado.

## Agendamento

O Hangfire guarda os jobs no próprio Postgres — sem Redis, sem mais um serviço
para operar. **A API é só cliente**: agenda e cancela, mas não executa nada. Quem
roda o servidor Hangfire é o worker, em container separado, e é por isso que
`docker compose up --scale worker=3` aumenta a capacidade de envio sem tocar na
API.

O horário é guardado em UTC **junto do fuso escolhido**. Só o instante não basta:
"amanhã às 9h" perde o sentido quando outra pessoa abre a tela de outro lugar.

O processador confere a hora antes de enviar, mesmo confiando no agendador.
E-mail que sai adiantado não tem como voltar, e a tolerância de um minuto existe
só para absorver diferença de relógio entre containers.

No projeto original dava para agendar, mas não para listar, cancelar ou
reagendar: você marcava e perdia o controle. As três operações existem aqui, com
uma regra — **disparo já processado não é reagendável**. Aceitar daria a
impressão de que a mensagem não saiu, e quem operou tomaria a decisão errada.

## Fila de disparo

O RabbitMQ reparte as entregas de um disparo entre os workers. A divisão de
papéis é a seguinte:

- **Hangfire** decide *quando* o disparo sai;
- **RabbitMQ** decide *quantos* processam ao mesmo tempo.

O processador publica em lotes, paginando por cursor sobre o id da entrega.
Paginar por `OFFSET` não funcionaria: publicar não muda a situação da entrega,
então a consulta devolveria o mesmo primeiro lote para sempre — foi exatamente o
bug que apareceu com 120 destinatários e só no ambiente real, o que motivou o
teste com 250.

O consumo usa **ack manual** com prefetch configurável: a mensagem só sai da
fila depois do envio confirmado, e um worker morto no meio devolve o trabalho em
vez de perdê-lo. O que falha repetidamente vai para a fila morta em vez de
circular para sempre.

Com `RabbitMq__Enabled=false` o processador envia em processo, sequencialmente.
É o modo usado pelos testes de integração e por quem quer rodar sem fila.

## E-mail

O envio usa **MailKit** contra um SMTP de verdade. Em desenvolvimento, o
Mailpit: aceita e guarda as mensagens numa interface web (http://localhost:8025)
sem entregar a ninguém.

Os testes de integração sobem um Mailpit em container e conferem a mensagem
recebida. Um dublê em memória provaria que o código chama o remetente; só um
SMTP real prova que a mensagem sai bem formada — com o HTML renderizado, o
assunto com as variáveis substituídas e o destinatário certo.

O assunto também aceita variáveis, e cada pessoa recebe o conteúdo montado com
os campos dela: os valores do destinatário vencem os valores comuns do disparo,
e `nome` está sempre disponível.

## Painel

`GET /api/dashboard` devolve contagens do banco: modelos, destinatários,
listas, disparos agendados, e-mails entregues no total e nos últimos 30 dias,
mais os cinco disparos mais recentes.

O painel do projeto original exibia "taxa de abertura ~68%" e "735 aberturas
estimadas" escritos no código-fonte — números que não vinham de lugar nenhum e
davam a impressão de um sistema que media coisas que não media. Aqui, sistema
recém-instalado mostra zero, e há teste que garante isso.

Não existe taxa de abertura porque não existe rastreamento de abertura. Inventar
o número seria pior do que não ter.

## Logs

Serilog em console, estruturado: cada evento carrega os campos como dados, o que
permite responder "todos os disparos do usuário X" sem expressão regular sobre
texto solto. Uma linha por requisição, com rota, situação e duração.

As chamadas de `/health` são registradas em `Verbose` — o healthcheck do compose
consulta a cada dez segundos, para sempre, e afogaria o que importa.

Os eventos do worker carregam o nome da máquina: com três containers processando
o mesmo disparo, saber qual deles emitiu a linha é o que torna o log utilizável.

## Estado

Fases 0 a 7 concluídas: infraestrutura, autenticação, modelos, destinatários,
disparo, agendamento, fila e observabilidade. O sistema roda inteiro em
containers e envia de ponta a ponta.

Não implementado, por decisão: publicação em nuvem, rastreamento de abertura e
testes de ponta a ponta no navegador.

