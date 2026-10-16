using System.Globalization;

namespace PTL.Core.Distribution;

// Builds the Sample Packaging grid from the raw stored-procedure result sets - a direct port of the
// row-construction rules in legacy PackagingSheet.aspx.vb (BuildTableCombinedPackaging,
// BuildTablePartCombinedPackaging, BuildTableWeekly).
public static class PackagingSheetBuilder
{
    private static readonly CultureInfo UkCulture = CultureInfo.GetCultureInfo("en-GB");

    public static string PrintKey(Guid id, DateTime date) => $"{id:N}_{date:yyyyMMdd}";

    // Legacy DateTime.ToLongDateString / ToShortDateString on the UK-configured servers (Windows
    // en-GB long date is "dd MMMM yyyy"; ICU's own en-GB long date adds a weekday, so it is pinned).
    public static string LongDate(DateTime date) => date.ToString("dd MMMM yyyy", UkCulture);

    public static string ShortDate(DateTime date) => date.ToString("d", UkCulture);

    public static PackagingSheet Build(PackagingMode mode, int week, PackagingRawData raw, DateTime today) =>
        new(mode, week, raw.YearId, raw.MonthId, mode switch
        {
            PackagingMode.GroupAddress => BuildGroupAddress(raw),
            PackagingMode.ParticipantCombined => BuildParticipantCombined(raw),
            _ => BuildDaily(raw, today),
        });

    private static List<PackagingBlock> BuildGroupAddress(PackagingRawData raw)
    {
        var blocks = new List<PackagingBlock>();

        foreach (var date in raw.Groups.Select(g => g.DistributionDate.Date).Distinct())
        {
            var groups = raw.Groups.Where(g => g.DistributionDate.Date == date).ToList();
            var totalSets = 0;
            foreach (var group in groups)
            {
                totalSets += ItemsFor(raw, group.GroupAddressId, date).Sum(i => i.NumberOfSetsRequired);
            }

            if (totalSets <= 0)
            {
                continue;
            }

            foreach (var group in groups)
            {
                var schemes = ItemsFor(raw, group.GroupAddressId, date)
                    .Where(i => i.NumberOfSetsRequired > 0)
                    .Select(i => ToSchemeRow(i, ParticipantsFor(raw, group.GroupAddressId, i.MonthlyDistributionSchemeId, date)))
                    .ToList();

                blocks.Add(new PackagingBlock(
                    PrintKey(group.GroupAddressId, date), group.GroupAddressId, date, group.Identifier, group.PackingInfo,
                    DueToBePosted: false, ShowAllPacked: false, ShowAllPosted: false, schemes));
            }
        }

        return blocks;
    }

    private static List<PackagingBlock> BuildParticipantCombined(PackagingRawData raw)
    {
        var blocks = new List<PackagingBlock>();

        foreach (var participant in raw.Groups)
        {
            var date = participant.DistributionDate.Date;
            var items = raw.SchemeItems
                .Where(s => s.ParticipantId == participant.ParticipantId && s.DistributionDate.Date == date)
                .ToList();

            var prepComplete = items.All(s => s.PreparationComplete);
            var anyUnpacked = items.Any(s => s.PackagedDate is null);
            var anyUnposted = items.Any(s => s.PostedDate is null);

            blocks.Add(new PackagingBlock(
                PrintKey(participant.ParticipantId, date), participant.ParticipantId, date, $"{participant.LabCode}: {participant.LabName}", participant.PackingInfo,
                DueToBePosted: false,
                ShowAllPacked: anyUnpacked && prepComplete,
                ShowAllPosted: anyUnposted && prepComplete,
                items.Select(ToSchemeRow).ToList()));
        }

        return blocks;
    }

    private static List<PackagingBlock> BuildDaily(PackagingRawData raw, DateTime today)
    {
        var blocks = new List<PackagingBlock>();

        foreach (var day in raw.Groups.Select(g => g.DistributionDate.Date).Distinct())
        {
            var items = raw.Items.Where(i => i.DistributionDate.Date == day).ToList();
            if (items.Sum(i => i.NumberOfSetsRequired) <= 0)
            {
                continue;
            }

            // Legacy only inspects each scheme's FIRST participant when deciding the day is complete.
            var complete = items.All(i =>
            {
                var first = DailyParticipants(raw, i.MonthlyDistributionSchemeId, day).FirstOrDefault();
                return first is null || first.PostedDate is not null;
            });
            var dueToBePosted = !complete && day <= today.Date;

            foreach (var item in items.Where(i => i.NumberOfSetsRequired > 0))
            {
                blocks.Add(new PackagingBlock(
                    PrintKey(item.MonthlyDistributionSchemeId, day), item.MonthlyDistributionSchemeId, day, LongDate(day), string.Empty,
                    dueToBePosted, ShowAllPacked: false, ShowAllPosted: false,
                    [ToSchemeRow(item, DailyParticipants(raw, item.MonthlyDistributionSchemeId, day))]));
            }
        }

        return blocks;
    }

    private static IEnumerable<PackagingItemEntity> ItemsFor(PackagingRawData raw, Guid groupAddressId, DateTime date) =>
        raw.Items.Where(i => i.GroupAddressId == groupAddressId && i.DistributionDate.Date == date);

    private static List<PackagingParticipantEntity> ParticipantsFor(PackagingRawData raw, Guid groupAddressId, Guid schemeId, DateTime date) =>
        raw.Participants
            .Where(p => p.GroupAddressId == groupAddressId && p.MonthlyDistributionSchemeId == schemeId && p.DistributionDate.Date == date)
            .ToList();

    private static List<PackagingParticipantEntity> DailyParticipants(PackagingRawData raw, Guid schemeId, DateTime date) =>
        raw.Participants.Where(p => p.MonthlyDistributionSchemeId == schemeId && p.DistributionDate.Date == date).ToList();

    private static PackagingSchemeRow ToSchemeRow(PackagingItemEntity item, List<PackagingParticipantEntity> participants) =>
        new(
            item.MonthlyDistributionSchemeId,
            SchemeDescription(item.DistributionReference, item.DistributionReferenceSuffix, item.ScheduleCode, item.SchemeIdentifier, item.SchemeName, item.NumberOfSetsRequired),
            item.PreparationComplete,
            item.PackingInfo,
            CustomerPackingInfo: string.Empty,
            PSLevelPackingInfo: string.Empty,
            ImportPermitStatus: string.Empty,
            DistributionParticipantId: null,
            PackagedDate: null,
            PostedDate: null,
            TotalState(participants.All(p => p.PackagedDate is not null), item.PreparationComplete),
            TotalState(participants.All(p => p.PostedDate is not null), item.PreparationComplete),
            participants.Select(p => new PackagingParticipantRow(
                p.DistributionParticipantId,
                ParticipantDescription(p),
                p.PackagedDate,
                p.PostedDate,
                ImportPermitStatus(p.ImportExportLicenceRequired, p.ImportPermitReceived))).ToList());

    private static PackagingSchemeRow ToSchemeRow(PackagingSchemeItemEntity item) =>
        new(
            item.MonthlyDistributionSchemeId,
            SchemeDescription(item.DistributionReference, item.DistributionReferenceSuffix, item.ScheduleCode, item.SchemeIdentifier, item.SchemeName, item.NumberOfSetsRequired),
            item.PreparationComplete,
            item.PackingInfo,
            item.CustomerPackingInfo,
            PsLevelPackingInfo(item.PSLevelPackingInfo, item.ImportExportLicenceRequired, item.CustomsCertificateRequired),
            ImportPermitStatus(item.ImportExportLicenceRequired, item.ImportPermitReceived),
            item.DistributionParticipantId,
            item.PackagedDate,
            item.PostedDate,
            PackagingTotalState.None,
            PackagingTotalState.None,
            []);

    private static PackagingTotalState TotalState(bool complete, bool preparationComplete)
    {
        if (complete)
        {
            return PackagingTotalState.Complete;
        }

        return preparationComplete ? PackagingTotalState.Selectable : PackagingTotalState.None;
    }

    public static string SchemeDescription(string reference, string suffix, string scheduleCode, string identifier, string name, int sets) =>
        $"{reference}{suffix}/{scheduleCode} {identifier}: {name} - {sets} {SetsText(sets)}";

    public static string ParticipantDescription(PackagingParticipantEntity p)
    {
        var text = $"{p.LabCode}: {p.LabName} - {p.NumberOfSetsRequired} {SetsText(p.NumberOfSetsRequired)}";
        if (p.PackingInfo.Length > 0)
        {
            text += ": " + p.PackingInfo;
        }

        if (p.CustomerPackingInfo.Length > 0)
        {
            text += ", " + p.CustomerPackingInfo;
        }

        var psLevel = PsLevelPackingInfo(p.PSLevelPackingInfo, p.ImportExportLicenceRequired, p.CustomsCertificateRequired);
        if (psLevel.Length > 0)
        {
            text += ", " + psLevel;
        }

        return text;
    }

    // Legacy ParticipantInfo/SchemeItem.PSLevelPackingInfo: the Customs prefix is applied first, so
    // the Import/Export prefix ends up in front of it.
    public static string PsLevelPackingInfo(string info, bool importExportLicenceRequired, bool customsCertificateRequired)
    {
        var result = info;
        if (customsCertificateRequired)
        {
            result = "Customs Certificate Required. " + result;
        }

        if (importExportLicenceRequired)
        {
            result = "Import/Export Licence Required. " + result;
        }

        return result;
    }

    public static string ImportPermitStatus(bool licenceRequired, bool permitReceived)
    {
        if (!licenceRequired)
        {
            return "N/A";
        }

        return permitReceived ? "Yes" : "No";
    }

    private static string SetsText(int count) => count == 1 ? "Set" : "Sets";
}
