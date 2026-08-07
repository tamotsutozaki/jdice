using Jdice.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jdice.Infrastructure.Persistence.Configurations;

public sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("templates");

        builder.HasKey(template => template.Id);

        // Identificador gerado pelo domínio, não pelo banco. Ver comentário
        // equivalente em TemplateVersionConfiguration.
        builder.Property(template => template.Id).ValueGeneratedNever();

        builder.Property(template => template.Name)
            .HasMaxLength(Template.MaximumNameLength)
            .IsRequired();

        builder.Property(template => template.Category)
            .HasMaxLength(Template.MaximumCategoryLength)
            .IsRequired();

        // text[] nativo do Postgres: o Npgsql mapeia direto e dá para filtrar
        // com operadores de array em SQL, sem tabela de junção para algo que é
        // só um punhado de rótulos.
        builder.Property(template => template.Tags)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(template => template.CreatedBy).IsRequired();
        builder.Property(template => template.CreatedAt).IsRequired();
        builder.Property(template => template.UpdatedAt).IsRequired();
        builder.Property(template => template.ArchivedAt);

        builder.Ignore(template => template.IsArchived);
        builder.Ignore(template => template.CurrentVersion);

        // Busca por nome é o caminho mais usado da listagem.
        builder.HasIndex(template => template.Name);
        builder.HasIndex(template => template.Category);

        builder.HasMany(template => template.Versions)
            .WithOne()
            .HasForeignKey(version => version.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Template.Versions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
