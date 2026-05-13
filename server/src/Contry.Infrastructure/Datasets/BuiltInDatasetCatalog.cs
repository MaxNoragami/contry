using System.Security.Cryptography;
using System.Text;
using Contry.Domain.Datasets;
using Contry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Contry.Infrastructure.Datasets;

public sealed class BuiltInDatasetCatalog(ContryDbContext dbContext, IHostEnvironment hostEnvironment, TimeProvider timeProvider)
{
    private readonly ContryDbContext _dbContext = dbContext;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        var datasetsRoot = ResolveDatasetsRoot(_hostEnvironment.ContentRootPath);
        var trackedFiles = Directory
            .EnumerateFiles(datasetsRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var normalizedPaths = trackedFiles
            .Select(path => ToDocumentPath(datasetsRoot, path))
            .ToHashSet(StringComparer.Ordinal);

        var existingDocuments = await _dbContext.BuiltInDatasetDocuments.ToListAsync(cancellationToken);
        var existingByPath = existingDocuments.ToDictionary(document => document.Path, StringComparer.Ordinal);
        var now = _timeProvider.GetUtcNow();

        foreach (var filePath in trackedFiles)
        {
            var documentPath = ToDocumentPath(datasetsRoot, filePath);
            var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
            var checksum = $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}";
            var contentType = GetContentType(filePath);

            if (existingByPath.TryGetValue(documentPath, out var existingDocument))
            {
                if (existingDocument.Checksum == checksum && existingDocument.ContentType == contentType)
                {
                    continue;
                }

                existingDocument.Checksum = checksum;
                existingDocument.ContentType = contentType;
                existingDocument.Content = content;
                existingDocument.UpdatedAtUtc = now;
                continue;
            }

            _dbContext.BuiltInDatasetDocuments.Add(new BuiltInDatasetDocument
            {
                Path = documentPath,
                ContentType = contentType,
                Checksum = checksum,
                Content = content,
                UpdatedAtUtc = now,
            });
        }

        var documentsToDelete = existingDocuments
            .Where(document => !normalizedPaths.Contains(document.Path))
            .ToList();

        if (documentsToDelete.Count > 0)
        {
            _dbContext.BuiltInDatasetDocuments.RemoveRange(documentsToDelete);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<BuiltInDatasetDocument?> FindByPathAsync(string path, CancellationToken cancellationToken)
        => _dbContext.BuiltInDatasetDocuments.SingleOrDefaultAsync(document => document.Path == path, cancellationToken);

    private static string ResolveDatasetsRoot(string apiContentRoot)
        => Path.GetFullPath(Path.Combine(apiContentRoot, "..", "..", "datasets"));

    private static string ToDocumentPath(string rootPath, string filePath)
        => $"/datasets/{Path.GetRelativePath(rootPath, filePath).Replace(Path.DirectorySeparatorChar, '/')}";

    private static string GetContentType(string filePath)
        => Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".json" => "application/json; charset=utf-8",
            ".csv" => "text/csv; charset=utf-8",
            _ => "text/plain; charset=utf-8",
        };
}
