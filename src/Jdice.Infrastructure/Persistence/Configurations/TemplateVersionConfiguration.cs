using Jdice.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jdice.Infrastructure.Persistence.Configurations;

public sealed class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("template_versions");

        builder.HasKey(version => version.Id);

        // O identificador é gerado pelo domínio (UUIDv7 derivado da data de
        // criação), não pelo banco. Sem declarar isso, o EF assume a convenção
        // de chave gerada por ele e, ao encontrar uma versão nova já com Id
        // preenchido, conclui que ela existe — emitindo UPDATE no lugar de
        // INSERT e falhando por "0 linhas afetadas".
        builder.Property(version => version.Id).ValueGeneratedNever();

        builder.Property(version => version.TemplateId).IsRequired();
        builder.Property(version => version.Number).IsRequired();

        // Sem limite de tamanho: um e-mail em HTML com estilos embutidos passa
        // fácil de qualquer teto que parecesse generoso.
        builder.Property(version => version.Html)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(version => version.Variables)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(version => version.CreatedBy).IsRequired();
        builder.Property(version => version.CreatedAt).IsRequired();

        // A numeração é calculada em memória a partir da última versão, e duas
        // requisições simultâneas chegariam ao mesmo número. Quem de fato
        // impede a colisão é este índice — a aplicação trata a violação e
        // tenta de novo com o número seguinte.
        builder.HasIndex(version => new { version.TemplateId, version.Number })
            .IsUnique();
    }
}
