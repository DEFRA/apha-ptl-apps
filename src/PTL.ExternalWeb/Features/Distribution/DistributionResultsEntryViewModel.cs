namespace PTL.ExternalWeb.Features.Distribution;

public sealed class DistributionResultsEntryViewModel
{
    public Guid MonthlyDistributionSchemeId { get; set; }

    public Guid DistributionParticipantId { get; set; }

    public string Heading { get; set; } = string.Empty;

    public bool SaveFailed { get; set; }

    public List<DistributionResultsEntryTestViewModel> Tests { get; set; } = [];
}

public sealed class DistributionResultsEntryTestViewModel
{
    public Guid TestId { get; set; }

    public string TestName { get; set; } = string.Empty;

    public List<DistributionResultsEntryResultItemViewModel> ResultItems { get; set; } = [];

    public List<DistributionResultsEntryMethodItemViewModel> MethodItems { get; set; } = [];

    public List<DistributionResultsEntrySampleRowViewModel> Samples { get; set; } = [];

    public int ValueIndexFor(int sampleIndex, Guid testResultItemId) =>
        Samples[sampleIndex].Values.FindIndex(v => v.TestResultItemId == testResultItemId);
}

public sealed class DistributionResultsEntryResultItemViewModel
{
    public Guid TestResultItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FormatString { get; set; } = string.Empty;

    public string ReturnValueType { get; set; } = string.Empty;

    public bool IsDropdown => !FormatString.StartsWith('^');

    public IReadOnlyList<string> DropdownOptions =>
        IsDropdown ? FormatString.Split('|', StringSplitOptions.RemoveEmptyEntries) : [];

    public string InputFormatMessage => $"Input must be of format: {DistributionResultsEntryFormatMessages.For(ReturnValueType)}";
}

public sealed class DistributionResultsEntryMethodItemViewModel
{
    public Guid TestMethodItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FormatString { get; set; } = string.Empty;

    public string ReturnValueType { get; set; } = string.Empty;

    public string? Result { get; set; }

    public bool IsDropdown => !FormatString.StartsWith('^');

    public IReadOnlyList<string> DropdownOptions =>
        IsDropdown ? FormatString.Split('|', StringSplitOptions.RemoveEmptyEntries) : [];

    public string InputFormatMessage => $"Input must be of format: {DistributionResultsEntryFormatMessages.For(ReturnValueType)}";
}

internal static class DistributionResultsEntryFormatMessages
{
    public static string For(string? returnValueType) =>
        returnValueType?.Contains("date", StringComparison.OrdinalIgnoreCase) == true
            ? "dd/MM/yyyy"
            : returnValueType ?? string.Empty;
}

public sealed class DistributionResultsEntrySampleRowViewModel
{
    public Guid SampleId { get; set; }

    public string SampleNumber { get; set; } = string.Empty;

    public List<DistributionResultsEntrySampleValueViewModel> Values { get; set; } = [];
}

public sealed class DistributionResultsEntrySampleValueViewModel
{
    public Guid SampleResultItemId { get; set; }

    public Guid TestResultItemId { get; set; }

    public string? Result { get; set; }
}
