namespace PTL.Common.Graph;

/// <summary>A Microsoft Graph call could not be made or did not succeed. The message never contains credentials or response bodies.</summary>
public sealed class GraphException : Exception
{
    public GraphException(string message)
        : base(message)
    {
    }

    public GraphException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
