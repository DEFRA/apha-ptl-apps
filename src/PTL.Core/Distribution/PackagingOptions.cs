namespace PTL.Core.Distribution;

/// <summary>
/// GOV.UK Notify settings for the Distribution Posted email (legacy DistributionPostedEmail*.txt
/// templates). The connection itself is the shared <c>PTL.Core.Notifications.NotifyOptions</c>.
/// </summary>
public static class DistributionPostedNotificationOptions
{
    /// <summary>Fixed - the same template is used in every environment, so it is not configuration.</summary>
    public const string TemplateId = "2bb0e2d0-fa23-472e-947c-4524d14ad288";
}

/// <summary>Where the fixed Customs Pro-Forma template lives in the shared template bucket.</summary>
public sealed class PackagingCustomsOptions
{
    public const string SectionName = "Packaging:Customs";

    // Legacy appSettings CustomsDocumentTemplate in the TemplatesFolder.
    public string TemplateFileName { get; set; } = "Customs Pro-Forma Template v20.10.2020.docx";

    public string Folder { get; set; } = "customs";
}
