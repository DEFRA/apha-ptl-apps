namespace PTL.Core.Scheme;

// Thrown by SchemeService when SchemeValidator rejects a create/update; PTL.Api's
// SchemeController translates this into a 400 ValidationProblem response.
public sealed class SchemeValidationException(IReadOnlyList<SchemeValidationError> errors) : Exception("Scheme validation failed.")
{
    public IReadOnlyList<SchemeValidationError> Errors { get; } = errors;
}
