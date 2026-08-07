using Jdice.Application.Abstractions;

namespace Jdice.Infrastructure.Security;

/// <summary>
/// BCrypt com work factor 12. O salt é gerado por hash e embutido no próprio
/// resultado, então não existe coluna de salt separada.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <summary>
    /// Calculado uma única vez na construção. Como esta classe é singleton, o
    /// custo é pago no start da aplicação e não em cada login.
    /// </summary>
    private readonly Lazy<string> _referenceHash;

    public BcryptPasswordHasher()
    {
        _referenceHash = new Lazy<string>(
            () => Hash(Guid.CreateVersion7().ToString()),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string ReferenceHash => _referenceHash.Value;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception exception) when (
            exception is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        {
            // Hash corrompido, vazio ou em formato de outra biblioteca: trata
            // como senha errada em vez de derrubar o request com 500. A
            // biblioteca sinaliza cada um desses casos com um tipo diferente.
            return false;
        }
    }
}
