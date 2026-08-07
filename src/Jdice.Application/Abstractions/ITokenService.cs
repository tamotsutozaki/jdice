using Jdice.Domain.Users;

namespace Jdice.Application.Abstractions;

public interface ITokenService
{
    AccessToken Issue(User user);
}

/// <param name="Value">O JWT em si.</param>
/// <param name="ExpiresAt">Quando expira, em UTC — usado para o Max-Age do cookie.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
