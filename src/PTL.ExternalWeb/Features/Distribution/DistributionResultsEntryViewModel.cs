using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PTL.ExternalWeb.Features.Distribution;

public sealed class DistributionResultsEntryViewModel
{
    // Nullable so a post that omits it is detected instead of silently binding to Guid.Empty.
    public Guid? MonthlyDistributionSchemeId { get; set; }

    public string Heading { get; set; } = string.Empty;

    [BindRequired]
    public bool IsReadOnly { get; set; }

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

    public string InputFormatMessage => $"Input must be of format: {RequiredFormat}";

    public string RequiredFormat => DistributionResultsEntryFormatMessages.For(ReturnValueType);

    public string RequiredFormatHint => DistributionResultsEntryFormatMessages.Hint(IsDropdown, ReturnValueType);
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

    public string InputFormatMessage => $"Input must be of format: {RequiredFormat}";

    public string RequiredFormat => DistributionResultsEntryFormatMessages.For(ReturnValueType);

    public string RequiredFormatHint => DistributionResultsEntryFormatMessages.Hint(IsDropdown, ReturnValueType);
}

internal static class DistributionResultsEntryFormatMessages
{
    public const string DateFormat = "dd/mm/yyyy";

    public const string DropdownHint = "Drop down list";

    public static string For(string? returnValueType) =>
        returnValueType?.Contains("date", StringComparison.OrdinalIgnoreCase) == true
            ? DateFormat
            : returnValueType ?? string.Empty;

    public static string Hint(bool isDropdown, string? returnValueType) => isDropdown ? DropdownHint : For(returnValueType);
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
