using Microsoft.Extensions.Options;
using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;
using PTL.Core.Contract.Export.Templates;

namespace PTL.Core.Distribution;

public interface IPackagingOutputService
{
    // Null when the month has not been initialised.
    Task<IReadOnlyList<PackagingLabel>?> GetLabelsAsync(
        int yearId, int monthId, PackagingMode mode, int week, IReadOnlyCollection<string> selectedKeys, CancellationToken cancellationToken = default);

    Task<PackagingCustomsOutcome> GetCustomsDocumentAsync(
        int yearId, int monthId, PackagingMode mode, int week, IReadOnlyCollection<string> selectedKeys, CancellationToken cancellationToken = default);
}

// Print Labels and Print Customs - legacy ButtonPrintLabels_Click / ButtonPrintCustomsDocument_Click.
// Labels are returned as printable text lines instead of being sent to a server-side label printer.
public sealed class PackagingOutputService(
    IDistributionRepository distributionRepository,
    ITemplateStorageService templateStorage,
    ITemplateMergeService templateMergeService,
    IOptions<TemplateStorageOptions> templateStorageOptions,
    IOptions<PackagingCustomsOptions> customsOptions) : IPackagingOutputService
{
    private const string DocxExtension = ".docx";

    public async Task<IReadOnlyList<PackagingLabel>?> GetLabelsAsync(
        int yearId, int monthId, PackagingMode mode, int week, IReadOnlyCollection<string> selectedKeys, CancellationToken cancellationToken = default)
    {
        var raw = await LoadRawAsync(yearId, monthId, mode, week, cancellationToken);
        if (raw is null)
        {
            return null;
        }

        var sheet = PackagingSheetBuilder.Build(mode, week, raw, DateTime.MinValue);
        var selected = new HashSet<string>(selectedKeys, StringComparer.OrdinalIgnoreCase);
        var labels = new List<PackagingLabel>();

        foreach (var block in sheet.Blocks.Where(b => selected.Contains(b.PrintKey)))
        {
            switch (mode)
            {
                case PackagingMode.GroupAddress:
                    await AddGroupAddressLabelsAsync(labels, block, raw, cancellationToken);
                    break;
                case PackagingMode.ParticipantCombined:
                    await AddParticipantCombinedLabelsAsync(labels, block, cancellationToken);
                    break;
                default:
                    await AddDailyLabelsAsync(labels, block, raw, cancellationToken);
                    break;
            }
        }

        return labels;
    }

    // Legacy Group Address option: the group's own label, then one participant label per
    // participant per scheme (every scheme of the group, whether or not it is shown).
    private async Task AddGroupAddressLabelsAsync(
        List<PackagingLabel> labels, PackagingBlock block, PackagingRawData raw, CancellationToken cancellationToken)
    {
        AddLabel(labels, await distributionRepository.GetLabelGroupAddressAsync(block.KeyId, cancellationToken), isGroup: true);

        foreach (var item in raw.Items.Where(i => i.GroupAddressId == block.KeyId && i.DistributionDate.Date == block.Date))
        {
            foreach (var participant in raw.Participants.Where(p =>
                p.GroupAddressId == block.KeyId && p.MonthlyDistributionSchemeId == item.MonthlyDistributionSchemeId && p.DistributionDate.Date == block.Date))
            {
                AddLabel(labels, await distributionRepository.GetLabelParticipantAddressAsync(participant.ParticipantId, item.MonthlyDistributionSchemeId, cancellationToken), isGroup: false);
            }
        }
    }

    // Legacy Participant Combined option: the participant's own label, then one per scheme (which
    // picks up the participant-scheme contact name where there is one).
    private async Task AddParticipantCombinedLabelsAsync(List<PackagingLabel> labels, PackagingBlock block, CancellationToken cancellationToken)
    {
        AddLabel(labels, await distributionRepository.GetLabelParticipantAddressAsync(block.KeyId, Guid.Empty, cancellationToken), isGroup: false);

        foreach (var scheme in block.Schemes)
        {
            AddLabel(labels, await distributionRepository.GetLabelParticipantAddressAsync(block.KeyId, scheme.MonthlyDistributionSchemeId, cancellationToken), isGroup: false);
        }
    }

    private async Task AddDailyLabelsAsync(List<PackagingLabel> labels, PackagingBlock block, PackagingRawData raw, CancellationToken cancellationToken)
    {
        foreach (var participant in raw.Participants.Where(p => p.MonthlyDistributionSchemeId == block.KeyId && p.DistributionDate.Date == block.Date))
        {
            AddLabel(labels, await distributionRepository.GetLabelParticipantAddressAsync(participant.ParticipantId, block.KeyId, cancellationToken), isGroup: false);
        }
    }

    private static void AddLabel(List<PackagingLabel> labels, PackagingLabelAddressEntity? address, bool isGroup)
    {
        if (address is null)
        {
            return;
        }

        string[] candidates = isGroup
            ? [address.Address1, address.Address2, address.Address3, address.Address4, address.Address5, address.Country, address.Telephone]
            : [address.ContactName, address.Organisation, address.Address1, address.Address2, address.Address3, address.Address4, address.Address5, address.Country, address.Telephone];

        labels.Add(new PackagingLabel(candidates.Where(line => !string.IsNullOrWhiteSpace(line)).ToList()));
    }

    public async Task<PackagingCustomsOutcome> GetCustomsDocumentAsync(
        int yearId, int monthId, PackagingMode mode, int week, IReadOnlyCollection<string> selectedKeys, CancellationToken cancellationToken = default)
    {
        if (mode == PackagingMode.GroupAddress)
        {
            return Failure("Print Customs is not available for Group Address Packaging.");
        }

        var raw = await LoadRawAsync(yearId, monthId, mode, week, cancellationToken);
        if (raw is null)
        {
            return Failure("There are no samples to be packed.");
        }

        List<ContractDocumentMergeData> documents;
        string fileName;
        var monthName = System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat.GetMonthName(monthId);

        if (mode == PackagingMode.ParticipantCombined)
        {
            documents = BuildParticipantCombinedDocuments(raw);
            fileName = $"Combined packaging {monthName} {yearId} Week {week}{DocxExtension}";
        }
        else
        {
            var sheet = PackagingSheetBuilder.Build(mode, 0, raw, DateTime.MinValue);
            var selected = new HashSet<string>(selectedKeys, StringComparer.OrdinalIgnoreCase);
            var selectedSchemeIds = sheet.Blocks.Where(b => selected.Contains(b.PrintKey)).Select(b => b.KeyId).ToHashSet();
            if (selectedSchemeIds.Count == 0)
            {
                return Failure("Please select schemes to print");
            }

            documents = BuildDailyDocuments(raw, selectedSchemeIds);
            fileName = $"Daily packaging {monthName} {yearId}{DocxExtension}";
        }

        var template = await templateStorage.GetAsync(TemplateKey(), cancellationToken);
        if (template is null)
        {
            return Failure("The Customs Pro-Forma template could not be found.");
        }

        return new PackagingCustomsOutcome(true, null, fileName, templateMergeService.MergeTemplateContentMany(template, documents, cancellationToken));
    }

    private static PackagingCustomsOutcome Failure(string error) => new(false, error, string.Empty, []);

    private string TemplateKey()
    {
        var prefix = (templateStorageOptions.Value.Prefix ?? string.Empty).Trim('/');
        var path = $"{customsOptions.Value.Folder.Trim('/')}/{customsOptions.Value.TemplateFileName}";
        return prefix.Length == 0 ? path : $"{prefix}/{path}";
    }

    // Legacy ExecuteAllPartGroupedCustomsDocuments: one document per distinct participant for the week.
    private static List<ContractDocumentMergeData> BuildParticipantCombinedDocuments(PackagingRawData raw)
    {
        var documents = new List<ContractDocumentMergeData>();

        foreach (var participant in raw.Groups.DistinctBy(g => g.ParticipantId))
        {
            var schemes = raw.SchemeItems
                .Where(s => s.ParticipantId == participant.ParticipantId && s.DistributionDate.Date == participant.DistributionDate.Date)
                .Select(s => SchemeRegionRow(s.SchemeIdentifier, s.CustomsDocumentDescription, s.CustomsDocumentVolume, s.NumberOfSamples));

            documents.Add(Document(AddressFields(participant), participant.DistributionDate, participant.LabCode, schemes));
        }

        return documents;
    }

    // Legacy ExecuteAllDailyCustomsDocuments: one document per distinct participant across the
    // selected schemes.
    private static List<ContractDocumentMergeData> BuildDailyDocuments(PackagingRawData raw, HashSet<Guid> selectedSchemeIds)
    {
        var documents = new List<ContractDocumentMergeData>();
        var rows = raw.Participants.Where(p => selectedSchemeIds.Contains(p.MonthlyDistributionSchemeId)).ToList();

        foreach (var participant in rows.DistinctBy(p => p.ParticipantId))
        {
            var schemes = rows
                .Where(p => p.ParticipantId == participant.ParticipantId)
                .Select(p => (Participant: p, Item: raw.Items.FirstOrDefault(i => i.MonthlyDistributionSchemeId == p.MonthlyDistributionSchemeId && i.DistributionDate.Date == p.DistributionDate.Date)))
                .Where(x => x.Item is not null)
                .Select(x => SchemeRegionRow(x.Item!.SchemeIdentifier, x.Item.CustomsDocumentDescription, x.Item.CustomsDocumentVolume, x.Item.NumberOfSamples));

            documents.Add(Document(AddressFields(participant), participant.DistributionDate, participant.LabCode, schemes));
        }

        return documents;
    }

    private static Dictionary<string, string> SchemeRegionRow(string identifier, string description, string volume, int samples) =>
        new Dictionary<string, string>
        {
            ["SchemeIdentifier"] = identifier,
            ["CustomsDescription"] = description,
            ["CustomsVolume"] = volume,
            ["NumberOfSamples"] = samples.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

    private static Dictionary<string, string> AddressFields(PackagingGroupEntity p) =>
        AddressFields(p.ContactName, p.Organisation, [p.Address1, p.Address2, p.Address3, p.Address4, p.Address5], p.Telephone, p.Country);

    private static Dictionary<string, string> AddressFields(PackagingParticipantEntity p) =>
        AddressFields(p.ContactName, p.Organisation, [p.Address1, p.Address2, p.Address3, p.Address4, p.Address5], p.Telephone, p.Country);

    private static Dictionary<string, string> AddressFields(
        string contactName, string organisation, string[] addressLines, string telephone, string country) =>
        new()
        {
            ["ContactName"] = contactName,
            ["Organisation"] = organisation,
            ["AddressLine1"] = addressLines[0],
            ["AddressLine2"] = addressLines[1],
            ["AddressLine3"] = addressLines[2],
            ["AddressLine4"] = addressLines[3],
            ["AddressLine5"] = addressLines[4],
            ["Telephone"] = telephone,
            ["Country"] = country,
        };

    private static ContractDocumentMergeData Document(
        Dictionary<string, string> fields, DateTime distributionDate, string labCode, IEnumerable<IReadOnlyDictionary<string, string>> schemes)
    {
        fields["Date"] = PackagingSheetBuilder.ShortDate(distributionDate);
        fields["Labcode"] = labCode;

        return new ContractDocumentMergeData(
            fields,
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["FeePayingSchemes"] = schemes.ToList(),
            });
    }

    private async Task<PackagingRawData?> LoadRawAsync(int yearId, int monthId, PackagingMode mode, int week, CancellationToken cancellationToken)
    {
        var schedule = await distributionRepository.GetMonthlyDistributionSchedulesAsync(yearId, monthId, cancellationToken);
        return schedule is null
            ? null
            : await distributionRepository.GetPackagingSheetAsync(schedule.Value.MonthlyDistributionId, mode, week, cancellationToken);
    }
}
