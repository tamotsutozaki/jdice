using System.Text.Json.Serialization;
using Jdice.Api.Auth;
using Jdice.Api.Campaigns;
using Jdice.Api.Recipients;
using Jdice.Api.Setup;
using Jdice.Api.Templates;
using Jdice.Application;
using Jdice.Infrastructure;
using Jdice.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Faz o minimal API respeitar os DataAnnotations dos records de request
// (e-mail válido, senha com tamanho mínimo) devolvendo 400 automaticamente.
builder.Services.AddValidation();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Sem isto, "role": "Admin" não desserializa e o enum só aceitaria o
    // número da posição — contrato ruim para quem consome e frágil se alguém
    // reordenar o enum.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Corpo JSON malformado é erro de quem chamou, não falha do servidor: sem
// isto o ASP.NET deixa a BadHttpRequestException subir e vira 500.
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Cliente do Hangfire, sem servidor: a API agenda e cancela, mas não executa
// disparo nenhum. Quem processa é o Jdice.Worker, em container próprio.
builder.Services.AddJobScheduling(builder.Configuration);

builder.Services.AddJdiceAuthentication();
builder.Services.AddLoginRateLimiter(builder.Configuration);

// "live"  → o processo está de pé. Sem dependências externas.
// "ready" → o processo consegue atender: depende do Postgres.
// A separação existe para o healthcheck do compose não derrubar a API por
// indisponibilidade momentânea do banco, e para o teste de integração poder
// subir sem infraestrutura.
builder.Services
    .AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

const string AngularCorsPolicy = "angular";

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularCorsPolicy, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        // Sem isso o navegador não envia o cookie de sessão em requisição
        // cross-origin, que é o caso do `ng serve` na porta 4200.
        .AllowCredentials());
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Sem UseHttpsRedirection: dentro do container a API serve HTTP puro e quem
// termina TLS é o proxy na frente. Redirecionar aqui só quebraria o healthcheck.

app.UseCors(AngularCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapAuthEndpoints();
app.MapTemplateEndpoints();
app.MapRecipientEndpoints();
app.MapCampaignEndpoints();

// Desligável para que o teste de integração controle quando o banco é
// preparado — e para que, na Fase 4, o worker não dispute a aplicação das
// migrations com a API na subida do compose.
if (builder.Configuration.GetValue("Database:AutoMigrate", defaultValue: true))
{
    await app.InitializeAsync();
}

app.Run();

// Exposto para o WebApplicationFactory nos testes de integração.
public partial class Program;
