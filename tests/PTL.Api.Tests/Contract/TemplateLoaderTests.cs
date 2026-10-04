using PTL.Core.Contract.Document;

namespace PTL.Api.Tests.Contract;

public class TemplateLoaderTests
{
    [Fact]
    public void FileTemplateLoader_UsesConfiguredRootWithoutCopyingAnything()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ptl-template-loader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "ContractExampleTemplate.docx"), "template");

        try
        {
            var root = new FileTemplateLoader(tempRoot).GetTemplatesRoot();

            Assert.Equal(Path.GetFullPath(tempRoot), root);
            Assert.True(File.Exists(Path.Combine(root, "ContractExampleTemplate.docx")));
            Assert.Single(Directory.EnumerateFileSystemEntries(root));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public void FileTemplateLoader_DefaultsToDocumentsTemplatesBesideTheApplication()
    {
        var root = new FileTemplateLoader().GetTemplatesRoot();

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "Documents", "Templates"), root);
    }
}
