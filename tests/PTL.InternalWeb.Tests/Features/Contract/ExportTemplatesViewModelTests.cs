using PTL.Contracts.Contract;
using PTL.Core.Contract.Export.Templates;
using PTL.InternalWeb.Features.Contract;

namespace PTL.InternalWeb.Tests.Features.Contract;

public class ExportTemplatesViewModelTests
{
    [Fact]
    public void IsRenewalLetters_RenewalLettersDocumentType_IsTrue()
    {
        var model = new ExportTemplatesViewModel(ExportDocumentTypes.RenewalLetters, "Export Renewal Letters", "ExportRenewalLetters", [], false);

        Assert.True(model.IsRenewalLetters);
    }

    [Fact]
    public void IsRenewalLetters_OtherDocumentType_IsFalse()
    {
        var model = new ExportTemplatesViewModel(ExportDocumentTypes.Contracts, "Export Contracts", "ExportContracts", [], false);

        Assert.False(model.IsRenewalLetters);
    }

    [Fact]
    public void Instructions_NoTemplates_ShowsUploadMessage()
    {
        var model = new ExportTemplatesViewModel(ExportDocumentTypes.Contracts, "Export Contracts", "ExportContracts", [], false);

        Assert.Equal("No Mail Merge Templates have been uploaded.", model.Instructions);
    }

    [Fact]
    public void Instructions_HasTemplates_ShowsSelectionMessage()
    {
        var templates = new[] { new ExportTemplateResponse(Guid.NewGuid(), "Template.docx", DateTime.UtcNow, ExportDocumentTypes.Contracts, true) };
        var model = new ExportTemplatesViewModel(ExportDocumentTypes.Contracts, "Export Contracts", "ExportContracts", templates, true);

        Assert.Equal("The selected Template that will be used during the Mail Merge is highlighted in the table below.", model.Instructions);
    }
}
