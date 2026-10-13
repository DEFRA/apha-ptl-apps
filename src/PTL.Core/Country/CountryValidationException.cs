namespace PTL.Core.Country;

public sealed class CountryValidationException(IReadOnlyList<CountryValidationError> errors)
    : Exception("Country validation failed.")
{
    public IReadOnlyList<CountryValidationError> Errors { get; } = errors;
}
