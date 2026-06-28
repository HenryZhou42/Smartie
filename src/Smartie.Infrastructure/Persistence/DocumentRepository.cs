using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;
using Smartie.Infrastructure.Persistence;

namespace Smartie.Infrastructure.Persistence;

public sealed class DocumentRepository : IDocumentRepository
{
    private readonly SmartieDbContext _db;

    public DocumentRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Document>> ListAsync(
        Guid userId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Documents
            .AsNoTracking()
            .Where(d => d.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d =>
                d.Name.Contains(term) ||
                d.FileName.Contains(term) ||
                d.Extension.Contains(term));
        }

        return await query
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<Document?> FindAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        _db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);

    public async Task<DocumentStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var documents = await _db.Documents
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var recentCutoff = DateTimeOffset.UtcNow.AddDays(-7);
        var extracted = documents.Where(d => d.ExtractionStatus == DocumentExtractionStatus.Completed).ToList();
        var lastExtracted = extracted
            .OrderByDescending(d => d.ExtractedAt ?? d.UpdatedAt)
            .FirstOrDefault();

        return new DocumentStats(
            documents.Count,
            documents.Count(d => d.IsIndexed),
            documents.Sum(d => d.SizeBytes),
            documents.Count(d => d.UploadedAt >= recentCutoff),
            extracted.Count,
            extracted.Sum(d => (long)d.ExtractedLength),
            lastExtracted?.ExtractedAt,
            lastExtracted?.ExtractorUsed);
    }

    public async Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        _db.Documents.Add(document);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return document;
    }

    public async Task<Document?> UpdateNameAsync(
        Guid documentId,
        Guid userId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        document.Name = name;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return document;
    }

    public async Task<bool> DeleteAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return false;
        }

        _db.Documents.Remove(document);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<Document?> FindForUpdateAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
