namespace PTL.Core.Viewer;

public sealed class ViewerValidationException(IReadOnlyList<ViewerValidationError> errors)
    : Exception("Viewer validation failed.")
{
    public IReadOnlyList<ViewerValidationError> Errors { get; } = errors;
}
