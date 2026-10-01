using Dapper;
using PTL.Core.Contract.Export.Templates;
using PTL.Data.Infrastructure;

namespace PTL.Data.Contract.Export;

public sealed class UploadedTemplateRepository(IDbConnectionFactory connectionFactory) : IUploadedTemplateRepository
{
    public async Task<IReadOnlyList<UploadedTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<UploadedTemplate>("EXEC dbo.spgaUploadedFiles")).ToList();
    }

    public async Task<IReadOnlyList<UploadedTemplate>> GetByDocumentTypeAsync(string documentType, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<UploadedTemplate>(
            "EXEC dbo.spgUploadedFilesByDocumentType @DocumentType",
            new { DocumentType = documentType })).ToList();
    }

    public async Task CreateAsync(UploadedTemplate uploadedTemplate, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spiUploadedFile @FileId = @FileId, @Filename = @Filename, @UploadedDate = @UploadedDate, @DocumentType = @DocumentType, @Selected = @Selected",
            new
            {
                uploadedTemplate.FileId,
                uploadedTemplate.Filename,
                uploadedTemplate.UploadedDate,
                uploadedTemplate.DocumentType,
                uploadedTemplate.Selected
            });
    }

    public async Task SetSelectedAsync(Guid fileId, bool selected, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spuUploadedFile @FileId = @FileId, @Selected = @Selected",
            new { FileId = fileId, Selected = selected });
    }

    public async Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync("EXEC dbo.spdUploadedFile @FileId", new { FileId = fileId });
    }
}
