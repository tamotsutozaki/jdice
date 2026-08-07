namespace Jdice.Api.Auth;

/// <summary>
/// Centraliza o formato do cookie de sessão para que emissão e remoção nunca
/// divirjam — um cookie removido com atributos diferentes dos de emissão
/// simplesmente não é apagado pelo navegador.
/// </summary>
public static class AuthCookie
{
    public const string Name = "jdice_auth";

    public static CookieOptions Options(bool isDevelopment, DateTimeOffset? expiresAt = null) => new()
    {
        HttpOnly = true,

        // Em Development a API roda em http://localhost, e um cookie Secure
        // não seria enviado. Fora disso é sempre exigido.
        Secure = !isDevelopment,

        // O front é servido pela mesma origem (nginx), então Strict não atrapalha
        // navegação e corta CSRF sem precisar de token anti-forgery.
        SameSite = SameSiteMode.Strict,

        Path = "/",
        Expires = expiresAt
    };
}
