using Jdice.Application.Abstractions;
using Jdice.Application.Templates;
using Jdice.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jdice.Infrastructure.Persistence;

public sealed class TemplateRepository(JdiceDbContext context) : ITemplateRepository
{
    /// <summary>Código SQLSTATE do Postgres para violação de restrição única.</summary>
    private const string UniqueViolation = "23505";

    public async Task<IReadOnlyList<Template>> ListAsync(
        TemplateFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = context.Templates
            .Include(template => template.Versions)
            .AsQueryable();

        if (!filter.IncluirArquivados)
        {
            query = query.Where(template => template.ArchivedAt == null);
        }

        if (!string.IsNullOrWhiteSpace(filter.Busca))
        {
            var busca = filter.Busca.Trim();
            query = query.Where(template => EF.Functions.ILike(template.Name, $"%{busca}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.Categoria))
        {
            var categoria = filter.Categoria.Trim();
            query = query.Where(template => template.Category == categoria);
        }

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tag = filter.Tag.Trim();
            query = query.Where(template => template.Tags.Contains(tag));
        }

        return await query
            .OrderByDescending(template => template.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Template?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Templates
            .Include(template => template.Versions)
            .SingleOrDefaultAsync(template => template.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(
        string name,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default) =>
        context.Templates.AnyAsync(
            template => template.Name.ToLower() == name.ToLower()
                && (exceptId == null || template.Id != exceptId),
            cancellationToken);

    public async Task<IReadOnlyList<string>> ListCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        await context.Templates
            .Where(template => template.Category != "")
            .Select(template => template.Category)
            .Distinct()
            .OrderBy(categoria => categoria)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListTagsAsync(
        CancellationToken cancellationToken = default) =>
        // SelectMany sobre a coluna text[] vira um unnest no Postgres: uma
        // consulta só, sem trazer os modelos para desmontar as tags em memória.
        await context.Templates
            .SelectMany(template => template.Tags)
            .Distinct()
            .OrderBy(tag => tag)
            .ToListAsync(cancellationToken);

    public Task AddAsync(Template template, CancellationToken cancellationToken = default)
    {
        context.Templates.Add(template);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: UniqueViolation } postgres)
        {
            // A numeração de versão é calculada em memória, então duas
            // requisições simultâneas chegam ao mesmo número e uma delas
            // esbarra no índice único. Isso é conflito de concorrência, não
            // falha do servidor — quem chamou decide se tenta de novo.
            if (postgres.ConstraintName?.Contains("template_versions", StringComparison.Ordinal) == true)
            {
                throw new TemplateVersionConflictException();
            }

            throw;
        }
    }
}
