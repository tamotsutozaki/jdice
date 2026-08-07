using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Jdice.Infrastructure.Persistence;

/// <summary>
/// Verifica que a API consegue mesmo falar com o banco. A configuração é lida
/// por chamada, e não no registro, porque nem sempre ela já está montada quando
/// os serviços são registrados — ler cedo fazia o check ser pulado sob teste,
/// com o endpoint de readiness respondendo "saudável" sem verificar nada.
/// </summary>
public sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("Postgres");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Configuração ausente é indisponibilidade, não erro interno: quem
            // consulta readiness precisa de um "não estou pronto", não de um 500.
            return HealthCheckResult.Unhealthy("Connection string do Postgres não configurada.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Não foi possível consultar o Postgres.", exception);
        }
    }
}
