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

/// <summary>
/// The fixed Customs Pro-Forma template in the shared template bucket (legacy appSettings
/// CustomsDocumentTemplate). There is no upload feature, so it is not configuration.
/// </summary>
public static class PackagingCustomsTemplate
{
    public const string FileName = "Customs Pro-Forma Template v20.10.2020.docx";

    public const string Folder = "customs";
}
