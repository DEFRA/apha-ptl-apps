namespace PTL.Contracts.Distribution;

// Initialise this Month (legacy MenuDistributions.aspx -> MonthlyDistribution.CreateMonthlyDistribution).
// Initialised is false when the month already existed, in which case nothing was written.
public sealed record InitialiseMonthResult(bool Initialised);
