using System.Data;
using PTL.Core.Contract.Export.Templates;
using PTL.Data.Contract.Export;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Contract;

public class UploadedTemplateRepositoryTests
{
    public UploadedTemplateRepositoryTests() => DapperColumnMappings.Register();

    private const string GetAllSql = "EXEC dbo.spgaUploadedFiles";
    private const string GetByTypeSql = "EXEC dbo.spgUploadedFilesByDocumentType @DocumentType";
    private const string CreateSql = "EXEC dbo.spiUploadedFile @FileId = @FileId, @Filename = @Filename, @UploadedDate = @UploadedDate, @DocumentType = @DocumentType, @Selected = @Selected";
    private const string SelectSql = "EXEC dbo.spuUploadedFile @FileId = @FileId, @Selected = @Selected";
    private const string DeleteSql = "EXEC dbo.spdUploadedFile @FileId";

    private static (UploadedTemplateRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new UploadedTemplateRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static DataTable TemplateTable(Guid fileId, bool selected = false, bool includeDocumentType = true)
    {
        var table = new DataTable();
        table.Columns.Add("fldFileId", typeof(Guid));
        table.Columns.Add("fldFilename", typeof(string));
        table.Columns.Add("fldUploadedDate", typeof(DateTime));
        table.Columns.Add("fldSelectedTemplate", typeof(bool));

        if (includeDocumentType)
        {
            table.Columns.Add("fldDocumentType", typeof(string));
            table.Rows.Add(fileId, "Contract Template.docx", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), selected, "Contracts");
        }
        else
        {
            table.Rows.Add(fileId, "Contract Template.docx", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), selected);
        }

        return table;
    }

    [Fact]
    public async Task GetAllAsync_MapsEveryColumn()
    {
        var (repository, connection) = CreateRepository();
        var fileId = Guid.NewGuid();
        connection.RespondToQuery(GetAllSql, TemplateTable(fileId, selected: true));

        var result = await repository.GetAllAsync();

        var template = Assert.Single(result);
        Assert.Equal(fileId, template.FileId);
        Assert.Equal("Contract Template.docx", template.Filename);
        Assert.Equal("Contracts", template.DocumentType);
        Assert.True(template.Selected);
    }

    // spgUploadedFilesByDocumentType does not select fldDocumentType back.
    [Fact]
    public async Task GetByDocumentTypeAsync_ToleratesTheMissingDocumentTypeColumn()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByTypeSql, TemplateTable(Guid.NewGuid(), includeDocumentType: false));

        var result = await repository.GetByDocumentTypeAsync("Contracts");

        Assert.Equal(string.Empty, Assert.Single(result).DocumentType);
    }

    [Fact]
    public async Task CreateAsync_BindsEveryArgumentByName()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(CreateSql, -1);
        var fileId = Guid.NewGuid();

        await repository.CreateAsync(new UploadedTemplate
        {
            FileId = fileId,
            Filename = "Contract Template.docx",
            UploadedDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            DocumentType = "Contracts",
            Selected = false
        });

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == CreateSql);
        Assert.Equal(fileId, command.ParameterValue("FileId"));
        Assert.Equal("Contract Template.docx", command.ParameterValue("Filename"));
        Assert.Equal("Contracts", command.ParameterValue("DocumentType"));
        Assert.Equal(false, command.ParameterValue("Selected"));
    }

    [Fact]
    public async Task SetSelectedAsync_BindsBothArgumentsByName()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(SelectSql, -1);
        var fileId = Guid.NewGuid();

        await repository.SetSelectedAsync(fileId, true);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == SelectSql);
        Assert.Equal(fileId, command.ParameterValue("FileId"));
        Assert.Equal(true, command.ParameterValue("Selected"));
    }

    [Fact]
    public async Task DeleteAsync_ExecutesTheDeleteProcedure()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(DeleteSql, -1);

        await repository.DeleteAsync(Guid.NewGuid());

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == DeleteSql);
    }
}
