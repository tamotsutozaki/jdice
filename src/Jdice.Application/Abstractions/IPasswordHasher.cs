namespace Jdice.Application.Abstractions;

public interface IPasswordHasher
{
    /// <summary>
    /// Hash de referência, sem senha correspondente conhecida. Serve para
    /// verificar contra algo quando o e-mail informado não existe, gastando o
    /// mesmo tempo do caminho normal — sem isso, dá para descobrir quem tem
    /// conta medindo o tempo de resposta do login.
    /// </summary>
    /// <remarks>
    /// Calculado uma vez pela implementação, que é registrada como singleton.
    /// Gerá-lo por requisição custaria um hash inteiro a mais em todo login.
    /// </remarks>
    string ReferenceHash { get; }

    string Hash(string password);

    bool Verify(string password, string hash);
}
