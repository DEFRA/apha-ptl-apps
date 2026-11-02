namespace PTL.Core.Contract.ImportPermit;

// The two result sets of spgaContractOrderImportPermitForEmail - legacy
// PtaBusinessObjects.BusinessObjects.Email.ImportPermitCustomer / ImportPermitParticipant. The
// procedure decides eligibility, the day-offset window and de-duplication; these are read models.
public sealed class ImportPermitCustomerEntity
{
    public Guid ContractId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid MonthlyDistributionSchemeId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string QalNumber { get; set; } = string.Empty;

    public string DistributionReference { get; set; } = string.Empty;

    public string SchemeIdentifier { get; set; } = string.Empty;

    // True when tblImportPermitReminders already holds this contract/distribution at this offset.
    public bool ReminderSent { get; set; }
}

public sealed class ImportPermitParticipantEntity
{
    public Guid ContractId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid ParticipantId { get; set; }

    public Guid ParticipantSchemeId { get; set; }

    public Guid SchemeId { get; set; }

    public Guid MonthlyDistributionSchemeId { get; set; }

    public string SchemeName { get; set; } = string.Empty;

    public string QalNumber { get; set; } = string.Empty;

    public string ParticipantEmail { get; set; } = string.Empty;

    public string LabCode { get; set; } = string.Empty;

    public string DistributionReference { get; set; } = string.Empty;

    public string SchemeIdentifier { get; set; } = string.Empty;

    public bool ImportPermitReceived { get; set; }

    public DateTime? ImportPermitExpiry { get; set; }

    public DateTime? DistributionDate { get; set; }

    public DateTime? OverseasPostingDate { get; set; }
}

public sealed record ImportPermitReminderData(
    IReadOnlyList<ImportPermitCustomerEntity> Customers,
    IReadOnlyList<ImportPermitParticipantEntity> Participants)
{
    public static ImportPermitReminderData Empty { get; } = new([], []);
}

public sealed record ImportPermitReminderRunSummary(
    int CustomersDue,
    int CustomerEmailsSent,
    int ParticipantsDue,
    int ParticipantEmailsSent,
    int Skipped,
    int Failed)
{
    public static ImportPermitReminderRunSummary Empty { get; } = new(0, 0, 0, 0, 0, 0);
}
