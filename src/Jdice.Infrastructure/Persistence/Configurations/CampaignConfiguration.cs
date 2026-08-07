using System.Text.Json;
using Jdice.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jdice.Infrastructure.Persistence.Configurations;

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns");

        builder.HasKey(campaign => campaign.Id);
        builder.Property(campaign => campaign.Id).ValueGeneratedNever();

        builder.Property(campaign => campaign.Name).HasMaxLength(300).IsRequired();
        builder.Property(campaign => campaign.Subject)
            .HasMaxLength(Campaign.MaximumSubjectLength)
            .IsRequired();
        builder.Property(campaign => campaign.FromName).HasMaxLength(200).IsRequired();

        builder.Property(campaign => campaign.TemplateId).IsRequired();
        builder.Property(campaign => campaign.TemplateVersionId).IsRequired();
        builder.Property(campaign => campaign.TemplateVersionNumber).IsRequired();

        builder.Property(campaign => campaign.SharedValues)
            .HasColumnType("jsonb")
            .HasConversion(
                valores => JsonSerializer.Serialize(valores, JsonOptions),
                json => Desserializar(json),
                Comparador)
            .IsRequired();

        builder.Property(campaign => campaign.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(campaign => campaign.TimeZone).HasMaxLength(60).IsRequired();
        builder.Property(campaign => campaign.ScheduledFor).IsRequired();
        builder.Property(campaign => campaign.CreatedBy).IsRequired();
        builder.Property(campaign => campaign.CreatedAt).IsRequired();
        builder.Property(campaign => campaign.StartedAt);
        builder.Property(campaign => campaign.CompletedAt);
        builder.Property(campaign => campaign.JobId).HasMaxLength(100);

        builder.Ignore(campaign => campaign.IsScheduledForFuture);

        // O worker procura o que está para sair; sem índice isso viraria
        // varredura da tabela inteira a cada verificação.
        builder.HasIndex(campaign => new { campaign.Status, campaign.ScheduledFor });

        builder.HasMany(campaign => campaign.Deliveries)
            .WithOne()
            .HasForeignKey(delivery => delivery.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Campaign.Deliveries))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static IReadOnlyDictionary<string, string> Desserializar(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
        ?? new Dictionary<string, string>();

    private static readonly ValueComparer<IReadOnlyDictionary<string, string>> Comparador =
        new(
            (esquerda, direita) =>
                esquerda != null
                && direita != null
                && esquerda.Count == direita.Count
                && esquerda.All(par => direita.ContainsKey(par.Key) && direita[par.Key] == par.Value),
            valores => valores.Aggregate(
                0,
                (acumulado, par) => HashCode.Combine(acumulado, par.Key, par.Value)),
            valores => new Dictionary<string, string>(
                valores.ToDictionary(par => par.Key, par => par.Value)));
}

public sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("deliveries");

        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Id).ValueGeneratedNever();

        builder.Property(delivery => delivery.CampaignId).IsRequired();
        builder.Property(delivery => delivery.RecipientId).IsRequired();
        builder.Property(delivery => delivery.Email).HasMaxLength(320).IsRequired();

        builder.Property(delivery => delivery.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(delivery => delivery.Attempts).IsRequired();
        builder.Property(delivery => delivery.Error).HasMaxLength(500).IsRequired();
        builder.Property(delivery => delivery.CreatedAt).IsRequired();
        builder.Property(delivery => delivery.SentAt);

        // A mesma pessoa não recebe duas vezes o mesmo disparo — garantia do
        // banco, não da lógica: com vários workers em paralelo, a checagem em
        // memória não basta.
        builder.HasIndex(delivery => new { delivery.CampaignId, delivery.RecipientId })
            .IsUnique();

        // O worker busca as pendentes de um disparo.
        builder.HasIndex(delivery => new { delivery.CampaignId, delivery.Status });
    }
}
