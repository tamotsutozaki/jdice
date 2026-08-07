using System.Text.Json.Serialization;
using Serilog;
using Jdice.Api.Auth;
using Jdice.Api.Campaigns;
using Jdice.Api.Dashboard;
using Jdice.Api.Recipients;
using Jdice.Api.Setup;
using Jdice.Api.Templates;
using Jdice.Application;
using Jdice.Infrastructure;
using Jdice.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Log estruturado: cada evento carrega os campos como dados, e não embutidos
// numa frase. É o que permite responder "todos os disparos do usuário X" sem
// depender de expressão regular sobre texto solto.
//
// Os níveis ficam em `Serilog:MinimumLevel` do appsettings, e não na seção
// `Logging`: o AddSerilog substitui a ILoggerFactory, então os filtros do
// Microsoft.Extensions.Logging deixam de ser consultados — configurá-los ali
// não teria efeito nenhum, e o log do ASP.NET afogaria o resto.
builder.Services.AddSerilog((services, config) => config
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Aplicacao", "Jdice.Api")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

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

// Uma linha por requisição, com rota, situação e duração — em vez das três
// que o padrão emite.
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "{RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0} ms";

    // Ruído de verificação de saúde esconde o que importa: o compose consulta
    // a cada dez segundos, para sempre.
    options.GetLevel = (http, _, erro) =>
        erro is not null || http.Response.StatusCode >= 500
            ? Serilog.Events.LogEventLevel.Error
            : http.Request.Path.StartsWithSegments("/health")
                ? Serilog.Events.LogEventLevel.Verbose
                : Serilog.Events.LogEventLevel.Information;
});

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
app.MapDashboardEndpoints();

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
