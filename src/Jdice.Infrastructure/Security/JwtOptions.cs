using System.ComponentModel.DataAnnotations;

namespace Jdice.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Mínimo de bytes da chave HMAC-SHA256. Abaixo disso a assinatura é fraca.</summary>
    public const int MinimumSigningKeyBytes = 32;

    [Required]
    public string Issuer { get; set; } = "jdice";

    [Required]
    public string Audience { get; set; } = "jdice";

    /// <summary>
    /// Nunca tem valor padrão de propósito: um default no código vira o segredo
    /// de produção de quem esqueceu de configurar. Sem isso, a aplicação não sobe.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Jwt:SigningKey é obrigatório.")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(typeof(TimeSpan), "00:05:00", "24:00:00")]
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(8);
}
