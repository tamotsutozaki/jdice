using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Jdice.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Id de quem está autenticado. Só devolve <c>null</c> em rota anônima —
    /// nas protegidas o token já foi validado, e o claim está lá.
    /// </summary>
    public static Guid? UserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : null;
}
