using PTL.ApiClient;
using PTL.Contracts.CreateOrder;

namespace PTL.ExternalWeb.Tests.TestSupport;

internal sealed class FakeCreateOrderApiClient : ICreateOrderApiClient
{
    private static readonly string[] FinancialYearLabels = ["Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar"];
    private static readonly int[] FinancialYearNumbers = [4, 5, 6, 7, 8, 9, 10, 11, 12, 1, 2, 3];

    public CreateOrderEligibilityResponse? Eligibility { get; set; } = new(true, true, true);
    public Exception? EligibilityException { get; set; }
    public CreateOrderApiResult GetResult { get; set; } = new(CreateOrderApiStatus.Ok, SampleOrder());
    public CreateOrderApiResult ChangeResult { get; set; } = new(CreateOrderApiStatus.Ok, SampleOrder(selected: true));
    public CreateOrderApiStatus ClearStatus { get; set; } = CreateOrderApiStatus.Ok;

    public string? LastGetYear { get; private set; }
    public Guid? LastParticipantId { get; private set; }
    public string? LastChangeYear { get; private set; }
    public CreateOrderLineChangeRequest? LastChange { get; private set; }
    public string? LastClearYear { get; private set; }

    public Task<CreateOrderEligibilityResponse?> GetEligibilityAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        EligibilityException is null ? Task.FromResult(Eligibility) : Task.FromException<CreateOrderEligibilityResponse?>(EligibilityException);

    public Task<CreateOrderApiResult> GetAsync(Guid participantId, string year, CancellationToken cancellationToken = default)
    {
        LastParticipantId = participantId;
        LastGetYear = year;
        return Task.FromResult(GetResult);
    }

    public Task<CreateOrderApiResult> ChangeAsync(Guid participantId, string year, CreateOrderLineChangeRequest request, CancellationToken cancellationToken = default)
    {
        LastParticipantId = participantId;
        LastChangeYear = year;
        LastChange = request;
        return Task.FromResult(ChangeResult);
    }

    public Task<CreateOrderApiStatus> ClearAsync(Guid participantId, string year, CancellationToken cancellationToken = default)
    {
        LastParticipantId = participantId;
        LastClearYear = year;
        return Task.FromResult(ClearStatus);
    }

    public static readonly Guid ParticipatingSchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AvailableSchemeId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // One scheme already participated in, one commercially available (Monthly schedule). The available scheme offers
    // Apr and May; selected = true ticks both and prices it.
    public static CreateOrderResponse SampleOrder(bool selected = false, bool canProceed = false, bool participatingSelected = false)
    {
        var participating = Scheme(ParticipatingSchemeId, "PT100", "Chemistry", "Quarterly", selected: participatingSelected, held: true);
        var available = Scheme(AvailableSchemeId, "PT200", "Microbiology", "Monthly", selected, held: false);
        return new CreateOrderResponse(
            "2026/27",
            "£",
            "QAL/00001",
            "LAB001",
            false,
            [participating],
            [new CreateOrderScheduleGroupResponse("Monthly", [available])],
            selected ? 20m : 0m,
            selected ? 10m : 0m,
            selected ? 30m : 0m,
            canProceed || selected);
    }

    private static CreateOrderSchemeResponse Scheme(Guid id, string identifier, string name, string schedule, bool selected, bool held)
    {
        var months = FinancialYearLabels.Select((label, index) =>
        {
            var offered = label is "Apr" or "May";
            var isHeld = held && label == "Apr";
            return new CreateOrderMonthResponse(
                label,
                FinancialYearNumbers[index],
                offered,
                isHeld || (selected && offered),
                offered && !isHeld,
                isHeld);
        }).ToList();

        return new CreateOrderSchemeResponse(
            id,
            identifier,
            name,
            schedule,
            100m,
            selected,
            ImportPermitRequired: false,
            ImportPermitEnabled: true,
            PostagePrice: selected ? 10m : 0m,
            SchemePrice: selected ? 20m : 0m,
            TotalPrice: selected ? 30m : 0m,
            HasPendingMonths: selected,
            months);
    }
}
