namespace PTL.Contracts.Labels;

public sealed record LabelPrintRequest(IReadOnlyList<string> Lines);

public sealed record LabelPrintResponse(bool Printed);
