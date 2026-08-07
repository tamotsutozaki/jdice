using System.Text.Json;
using Jdice.Domain.Recipients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jdice.Infrastructure.Persistence.Configurations;

public sealed class RecipientConfiguration : IEntityTypeConfiguration<Recipient>
{
    public void Configure(EntityTypeBuilder<Recipient> builder)
    {
        builder.ToTable("recipients");

        builder.HasKey(recipient => recipient.Id);
        builder.Property(recipient => recipient.Id).ValueGeneratedNever();

        builder.Property(recipient => recipient.Email)
            .HasMaxLength(320)
            .IsRequired();

        // A unicidade do destinatário é garantia do banco, não da checagem em
        // memória: a importação processa muitas linhas e duas requisições
        // simultâneas passariam pela verificação prévia.
        builder.HasIndex(recipient => recipient.Email).IsUnique();

        builder.Property(recipient => recipient.Name)
            .HasMaxLength(Recipient.MaximumNameLength)
            .IsRequired();

        // Campos livres em jsonb: a planilha de cada cliente traz colunas
        // diferentes, e criar coluna para cada uma seria inviável. Em jsonb o
        // Postgres ainda permite consultar por chave se um dia precisar.
        builder.Property(recipient => recipient.Fields)
            .HasColumnType("jsonb")
            .HasConversion(
                campos => JsonSerializer.Serialize(campos, JsonOptions),
                json => Desserializar(json),
                ComparadorDeCampos)
            .IsRequired();

        builder.Property(recipient => recipient.UnsubscribedAt);
        builder.Property(recipient => recipient.CreatedAt).IsRequired();
        builder.Property(recipient => recipient.UpdatedAt).IsRequired();

        builder.Ignore(recipient => recipient.IsSubscribed);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // O conteúdo é dado de cliente com acentuação; escapar tudo deixaria o
        // jsonb ilegível numa consulta manual.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static IReadOnlyDictionary<string, string> Desserializar(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
        ?? new Dictionary<string, string>();

    /// <summary>
    /// Sem comparador, o EF não percebe alteração dentro do dicionário — ele
    /// compararia por referência e uma mudança de campo nunca seria gravada.
    /// </summary>
    private static readonly ValueComparer<IReadOnlyDictionary<string, string>> ComparadorDeCampos =
        new(
            (esquerda, direita) =>
                esquerda != null
                && direita != null
                && esquerda.Count == direita.Count
                && esquerda.All(par =>
                    direita.ContainsKey(par.Key) && direita[par.Key] == par.Value),
            campos => campos.Aggregate(
                0,
                (acumulado, par) => HashCode.Combine(acumulado, par.Key, par.Value)),
            campos => new Dictionary<string, string>(
                campos.ToDictionary(par => par.Key, par => par.Value),
                StringComparer.OrdinalIgnoreCase));
}
