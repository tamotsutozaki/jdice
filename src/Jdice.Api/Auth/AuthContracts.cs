using System.ComponentModel.DataAnnotations;
using Jdice.Domain.Users;

namespace Jdice.Api.Auth;

public sealed record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Senha);

public sealed record CreateUserRequest(
    [property: Required, EmailAddress, MaxLength(320)] string Email,
    [property: Required,
        MinLength(PasswordPolicy.MinimumLength),
        MaxLength(PasswordPolicy.MaximumLength)] string Senha,
    [property: Required] UserRole Role);

public sealed record ChangePasswordRequest(
    [property: Required] string SenhaAtual,
    [property: Required,
        MinLength(PasswordPolicy.MinimumLength),
        MaxLength(PasswordPolicy.MaximumLength)] string NovaSenha);

/// <summary>
/// O que o front sabe sobre quem está logado. Não inclui o token: ele viaja
/// em cookie httpOnly e o JavaScript não deve conseguir lê-lo.
/// </summary>
public sealed record CurrentUserResponse(Guid Id, string Email, string Role);

public sealed record CreatedUserResponse(Guid Id, string Email, string Role);

public sealed record UserListItemResponse(
    Guid Id,
    string Email,
    string Role,
    bool Ativo,
    DateTimeOffset CriadoEm,
    DateTimeOffset? DesativadoEm);
