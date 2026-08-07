using System.Reflection;

namespace Jdice.UnitTests;

/// <summary>
/// Guarda a direção das dependências entre camadas. A separação em projetos
/// já impede o pior, mas nada impede alguém de instalar um pacote de
/// infraestrutura direto no Domain ou no Application — é isso que estes
/// testes pegam.
/// </summary>
public class ArquiteturaDeCamadasTests
{
    private static readonly string[] AssembliesDeInfraestrutura =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.AspNetCore",
        "Hangfire",
        "RabbitMQ",
        "MailKit",
        "Scriban"
    ];

    [Fact]
    public void Domain_nao_depende_de_nenhum_outro_projeto_da_solucao()
    {
        var referencias = ReferenciasDe(typeof(Domain.AssemblyMarker));

        var referenciasInternas = referencias
            .Where(nome => nome.StartsWith("Jdice.", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(referenciasInternas);
    }

    [Fact]
    public void Domain_nao_depende_de_infraestrutura()
    {
        AssertSemInfraestrutura(typeof(Domain.AssemblyMarker));
    }

    [Fact]
    public void Application_nao_depende_de_infraestrutura()
    {
        AssertSemInfraestrutura(typeof(Application.AssemblyMarker));
    }

    [Fact]
    public void Application_nao_depende_de_nada_da_solucao_alem_de_Domain()
    {
        // Asserção pela negativa de propósito: o compilador só emite a referência
        // de assembly quando algum tipo é de fato usado, então exigir que
        // "Jdice.Domain" apareça daria falso negativo enquanto as camadas
        // estiverem vazias. O que precisa ser verdade sempre é que nada mais
        // apareça — em especial Jdice.Infrastructure.
        var referenciasIndevidas = ReferenciasDe(typeof(Application.AssemblyMarker))
            .Where(nome => nome.StartsWith("Jdice.", StringComparison.Ordinal))
            .Where(nome => nome != "Jdice.Domain")
            .ToArray();

        Assert.Empty(referenciasIndevidas);
    }

    private static void AssertSemInfraestrutura(Type ancora)
    {
        var proibidas = ReferenciasDe(ancora)
            .Where(nome => AssembliesDeInfraestrutura.Any(proibido =>
                nome.StartsWith(proibido, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.Empty(proibidas);
    }

    private static string[] ReferenciasDe(Type ancora) =>
        ancora.Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();
}
