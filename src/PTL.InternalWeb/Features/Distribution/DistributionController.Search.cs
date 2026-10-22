using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PTL.Contracts.Search;

namespace PTL.InternalWeb.Features.Distribution;

// Distribution Search - legacy Distributions/Search.aspx ("This page can be used to search for
// Schemes."). Two tabs: Distribution Number (optional, blank = every distribution) and Sample
// Number (mandatory). Authorization is the class-level PtlPolicies.Distributions only - legacy
// Search.aspx sits directly in the Distributions/ folder, not under Scheduling/ or Internal User/,
// so no narrower role is required here (unlike most of this controller's other actions).
public sealed partial class DistributionController
{
    // GET so a result set survives following a Details link and coming back. tab is absent on the
    // first visit (nothing searched); each tab's Search button submits only its own criteria with
    // its tab, mirroring Features/Search/SearchController's Customers/Participants pattern.
    // MVC1004 suppressed: the explicit [Bind(Prefix = ...)] already fixes the exact query-string
    // prefix each form binds from, so the parameter/property name overlap the analyzer warns about
    // cannot cause ambiguous binding here.
#pragma warning disable MVC1004
    [HttpGet]
    public async Task<IActionResult> Search(
        DistributionSearchTab? tab,
        int page,
        [Bind(Prefix = nameof(DistributionSearchViewModel.DistributionNumber))] DistributionNumberSearchForm? distributionNumberForm,
        [Bind(Prefix = nameof(DistributionSearchViewModel.SampleNumber))] SampleNumberSearchForm? sampleNumberForm,
        CancellationToken cancellationToken)
#pragma warning restore MVC1004
    {
        var distributionNumber = distributionNumberForm ?? new DistributionNumberSearchForm();
        var sampleNumber = sampleNumberForm ?? new SampleNumberSearchForm();

        // Each tab's form submits only its own fields. Model binding/validation runs for both
        // forms on every request regardless of which tab was searched, so the other tab's errors
        // (e.g. the Sample Number tab's required-field error on first load, before Search is ever
        // clicked) must be discarded here - otherwise they would always show, even unsearched.
        if (tab != DistributionSearchTab.SampleNumber)
        {
            ModelState.Remove($"{nameof(DistributionSearchViewModel.SampleNumber)}.{nameof(SampleNumberSearchForm.SampleNumber)}");
        }

        if (tab != DistributionSearchTab.DistributionNumber)
        {
            ModelState.Remove($"{nameof(DistributionSearchViewModel.DistributionNumber)}.{nameof(DistributionNumberSearchForm.DistributionNumber)}");
        }

        var isValid = ModelState.IsValid;

        var distributionNumberResults = tab == DistributionSearchTab.DistributionNumber && isValid
            ? await distributionApiClient.SearchByDistributionNumberAsync(distributionNumber.ToCriteria(), cancellationToken)
            : null;

        var sampleNumberPage = page < 1 ? 1 : page;
        IReadOnlyList<SampleNumberSearchResultResponse>? sampleNumberResults = null;
        var sampleNumberTotalCount = 0;

        if (tab == DistributionSearchTab.SampleNumber && isValid)
        {
            var allResults = await distributionApiClient.SearchBySampleNumberAsync(sampleNumber.ToCriteria(), cancellationToken);
            sampleNumberTotalCount = allResults.Count;
            sampleNumberResults = allResults
                .Skip((sampleNumberPage - 1) * DistributionSearchViewModel.SampleNumberPageSize)
                .Take(DistributionSearchViewModel.SampleNumberPageSize)
                .ToList();
        }

        return View(new DistributionSearchViewModel
        {
            ActiveTab = tab ?? DistributionSearchTab.DistributionNumber,
            DistributionNumber = distributionNumber,
            SampleNumber = sampleNumber,
            DistributionNumberResults = distributionNumberResults,
            SampleNumberResults = sampleNumberResults,
            SampleNumberTotalCount = sampleNumberTotalCount,
            SampleNumberPage = sampleNumberPage,
        });
    }
}
