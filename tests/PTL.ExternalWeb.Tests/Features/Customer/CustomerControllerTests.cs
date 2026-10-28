using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Customer;
using PTL.ExternalWeb.Features.Customer;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Customer;

// Unit-level tests for the controller's own branching (view selection, error-dictionary mapping) -
// see Integration/CustomerRouteSmokeTests.cs for full-pipeline coverage of the real participant
// claims/authorization/rendering.
public class CustomerControllerTests
{
    private static CustomerController CreateController(FakeCustomerDetailsEditApiClient? apiClient = null, FakeLookupApiClient? lookupApiClient = null) =>
        new(apiClient ?? new FakeCustomerDetailsEditApiClient(), lookupApiClient ?? new FakeLookupApiClient());

    [Fact]
    public async Task Index_Post_ValidationFailure_AddsModelStateErrorsFromApiResult()
    {
        var apiClient = new FakeCustomerDetailsEditApiClient
        {
            SubmitResult = new CustomerDetailsEditSaveResult(false, new Dictionary<string, string[]> { ["ContactName"] = ["Enter Contact Name"] })
        };
        var controller = CreateController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Index(new CustomerDetailsEditViewModel(), CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<CustomerDetailsEditViewModel>(viewResult.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState["ContactName"]!.Errors, e => e.ErrorMessage == "Enter Contact Name");
    }

    [Fact]
    public async Task Index_Post_SuccessfulSubmission_RedirectsBackToIndex()
    {
        var apiClient = new FakeCustomerDetailsEditApiClient { SubmitResult = new CustomerDetailsEditSaveResult(true, new Dictionary<string, string[]>()) };
        var controller = CreateController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Index(new CustomerDetailsEditViewModel { ContactName = "Jane Doe" }, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(CustomerController.Index), redirect.ActionName);
    }
}
