namespace PTL.InternalWeb.Features.Distribution;

// Sample Receipt screen - legacy Internal User/SampleReceipt.aspx (sitemap title "Submission Status").
// Reached from the Results Receipt screen's per-participant Submission Status "View/Edit" link.
public sealed class SampleReceiptViewModel
{
    // S6964 suppressed: the route ids are carried in the form action, not bound from the body.
#pragma warning disable S6964
    public int YearId { get; set; }

    public int MonthId { get; set; }

    public Guid MonthlyDistributionSchemeId { get; set; }

    public Guid DistributionParticipantId { get; set; }
#pragma warning restore S6964

    // Legacy LblDistributionSchemeReference / LblLabCode - re-read from the API, never posted.
    public string? SchemeHeading { get; set; }

    public string? LabHeading { get; set; }

    // No Tests, or the scheme is ready for the assessor: legacy disables the tick boxes and Save/Apply.
    public bool IsReadOnly { get; set; }

    public List<SampleReceiptTestViewModel> Tests { get; set; } = [];
}

public sealed class SampleReceiptTestViewModel
{
    public Guid TestId { get; set; }

    public string? TestType { get; set; }

    public List<SampleReceiptSampleViewModel> Samples { get; set; } = [];
}

public sealed class SampleReceiptSampleViewModel
{
    public Guid SampleReceiptId { get; set; }

    public string? SampleNumber { get; set; }

    // Legacy "Submitted", "Not submitted" and "Not in reportoire" tick boxes (mutually exclusive).
    public bool Submitted { get; set; }

    public bool NotSubmitted { get; set; }

    public bool NotInRepertoire { get; set; }
}
