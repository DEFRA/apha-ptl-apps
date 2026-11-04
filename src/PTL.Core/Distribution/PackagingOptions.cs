namespace PTL.Core.Distribution;

/// <summary>
/// Replaces the legacy Windows service's SendDistributionPostedEmails switch. Off keeps the emails queued in
/// tblDistributionPostedEmail: posting then sends nothing.
/// </summary>
public sealed class DistributionPostedEmailOptions
{
    public const string SectionName = "DistributionPostedEmails";

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// The GOV.UK Notify template of the Distribution Posted email (legacy DistributionPostedEmail*.txt templates). Fixed - the
/// same template is used in every environment, so it is not configuration. The connection itself is the shared
/// <c>PTL.Core.Notifications.NotifyOptions</c>.
/// </summary>
public static class DistributionPostedEmailTemplates
{
    public const string Name = "distribution-posted";
    public const string Id = "2bb0e2d0-fa23-472e-947c-4524d14ad288";
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
