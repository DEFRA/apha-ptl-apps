using PTL.Contracts.Contract;

namespace PTL.Core.Contract.Document;

public sealed class ContractDocumentService(ITemplateRepository templateRepository, ITemplateMergeService templateMergeService) : IContractDocumentService
{
    public async Task<ContractDocumentResponse> GenerateAsync(ContractDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var template = await templateRepository.GetAsync(request.DocumentType, request.TemplateName, cancellationToken)
            ?? throw new FileNotFoundException($"No template was found for document type '{request.DocumentType}'.");

        if (!File.Exists(template.TemplatePath))
        {
            throw new FileNotFoundException($"Template '{template.TemplateName}' was not found at '{template.TemplatePath}'.");
        }

        var mergedBytes = request.AdditionalDocuments is { Count: > 0 }
            ? templateMergeService.MergeTemplateMany(
                template.TemplatePath,
                [new ContractDocumentMergeData(request.MergeValues, request.Regions), .. request.AdditionalDocuments],
                cancellationToken)
            : templateMergeService.MergeTemplate(template.TemplatePath, request.MergeValues, request.Regions, cancellationToken);

        return new ContractDocumentResponse(
            string.IsNullOrWhiteSpace(request.FileName) ? template.TemplateName + ".docx" : request.FileName,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            mergedBytes);
    }
}
