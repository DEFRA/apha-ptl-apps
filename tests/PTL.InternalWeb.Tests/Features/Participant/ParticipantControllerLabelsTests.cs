using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Lookup;
using PTL.Contracts.Participant;
using PTL.InternalWeb.Features.Participant;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Participant;

public class ParticipantControllerLabelsTests
{
    private static ParticipantController CreateController(FakeParticipantApiClient? participantApiClient = null, FakeLookupApiClient? lookupApiClient = null, FakeCustomerApiClient? customerApiClient = null) =>
        new(participantApiClient ?? new FakeParticipantApiClient(), customerApiClient ?? new FakeCustomerApiClient(), lookupApiClient ?? new FakeLookupApiClient(),
            NullLogger<ParticipantController>.Instance);

    private static ParticipantResponse SampleParticipant(Guid participantId, Guid customerId, Guid countryId) => new(
        participantId, Guid.NewGuid(), customerId, "LAB-01", "Sample Laboratory", Guid.NewGuid(), "Alice Example",
        "Sample Laboratory Ltd", "1 Sample Street", "Sample District", string.Empty, string.Empty, string.Empty,
        countryId, "01234 567890", string.Empty, "alice@example.com", string.Empty, "Sample comments", true, null, false, null);

    [Fact]
    public async Task PrintAddressLabel_Get_UnknownParticipant_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantApiClient { ParticipantResponse = null });

        var result = await controller.PrintAddressLabel(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PrintAddressLabel_Get_ExistingParticipant_ReturnsAddressLabelPdfWithMatchedCountry()
    {
        var participantId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var participantApiClient = new FakeParticipantApiClient { ParticipantResponse = SampleParticipant(participantId, Guid.NewGuid(), countryId) };
        var lookupApiClient = new FakeLookupApiClient { Countries = [new CountryResponse(countryId, "United Kingdom")] };
        var controller = CreateController(participantApiClient, lookupApiClient);

        var result = await controller.PrintAddressLabel(participantId, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("address-label.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task PrintAddressLabel_Get_CountryNotInLookup_RendersWithBlankCountry()
    {
        var participantId = Guid.NewGuid();
        var participantApiClient = new FakeParticipantApiClient { ParticipantResponse = SampleParticipant(participantId, Guid.NewGuid(), Guid.NewGuid()) };
        var controller = CreateController(participantApiClient, new FakeLookupApiClient { Countries = [] });

        var result = await controller.PrintAddressLabel(participantId, CancellationToken.None);

        Assert.IsType<FileContentResult>(result);
    }

    [Fact]
    public async Task PrintAddressLabel_Post_InvalidModelState_ExistingParticipant_RedisplaysEditForm()
    {
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeParticipantApiClient { ParticipantResponse = SampleParticipant(participantId, customerId, Guid.NewGuid()) });
        controller.ModelState.AddModelError("LabName", "Enter a lab name.");
        var model = new ParticipantFormViewModel { ParticipantId = participantId, CustomerId = customerId, LabCode = "LAB-01" };

        var result = await controller.PrintAddressLabel(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(ParticipantController.Edit), view.ViewName);
    }

    [Fact]
    public async Task PrintAddressLabel_Post_InvalidModelState_NewParticipantWithoutLabCode_RedisplaysCreateFormWithGeneratedLabCode()
    {
        var customerId = Guid.NewGuid();
        var controller = CreateController();
        controller.ModelState.AddModelError("ContactName", "Enter a contact name.");
        var model = new ParticipantFormViewModel { CustomerId = customerId, LabCode = null };

        var result = await controller.PrintAddressLabel(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(ParticipantController.Create), view.ViewName);
        Assert.False(string.IsNullOrWhiteSpace(((ParticipantFormViewModel)view.Model!).LabCode));
    }

    [Fact]
    public async Task PrintAddressLabel_Post_ValidModel_ReturnsAddressLabelPdf()
    {
        var countryId = Guid.NewGuid();
        var controller = CreateController(lookupApiClient: new FakeLookupApiClient { Countries = [new CountryResponse(countryId, "United Kingdom")] });
        var model = new ParticipantFormViewModel
        {
            CustomerId = Guid.NewGuid(),
            ContactName = "Alice Example",
            Organisation = "Sample Laboratory Ltd",
            Address1 = "1 Sample Street",
            CountryId = countryId,
            Telephone = "01234 567890"
        };

        var result = await controller.PrintAddressLabel(model, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("address-label.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task GenerateLabCodeAsync_ExistingNumericCodeInRange_IsSkippedAndNonNumericCodesAreIgnored()
    {
        var customerId = Guid.NewGuid();
        var participantApiClient = new FakeParticipantApiClient
        {
            SearchResponse = new ParticipantSearchResponse(
                [
                    new ParticipantSummaryResponse(Guid.NewGuid(), customerId, "1000", "Existing Lab", "Contact", true),
                    new ParticipantSummaryResponse(Guid.NewGuid(), customerId, "NOT-NUMERIC", "Other Lab", "Contact", true)
                ],
                2, 1, 20)
        };
        var controller = CreateController(participantApiClient);

        var result = await controller.Create(customerId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantFormViewModel>(view.Model);
        Assert.NotEqual("1000", model.LabCode);
    }
}
