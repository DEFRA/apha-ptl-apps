namespace PTL.Core.Contract.Document;

public sealed record DocumentTemplate(
    string TemplateName,
    string DocumentType,
    string TemplateKey,
    string TemplatePath);
