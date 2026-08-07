using HealthChecks.NpgSql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// "live"  → o processo está de pé. Sem dependências externas.
// "ready" → o processo consegue atender: depende do Postgres.
// A separação existe para o healthcheck do compose não derrubar a API por
// indisponibilidade momentânea do banco, e para o teste de integração poder
// subir sem infraestrutura.
var healthChecks = builder.Services.AddHealthChecks();

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");

if (!string.IsNullOrWhiteSpace(postgresConnectionString))
{
    healthChecks.AddNpgSql(postgresConnectionString, name: "postgres", tags: ["ready"]);
}

const string AngularCorsPolicy = "angular";

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularCorsPolicy, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Sem UseHttpsRedirection: dentro do container a API serve HTTP puro e quem
// termina TLS é o proxy na frente. Redirecionar aqui só quebraria o healthcheck.

app.UseCors(AngularCorsPolicy);

app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

// Exposto para o WebApplicationFactory nos testes de integração.
public partial class Program;
