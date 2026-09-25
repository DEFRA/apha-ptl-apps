using Dapper;
using PTL.Core.Participant;
using PTL.Data.Infrastructure;

namespace PTL.Data.Participant;

public sealed class ParticipantSchemeRepository(IDbConnectionFactory connectionFactory) : IParticipantSchemeRepository
{
    public async Task<ParticipantSchemeRecord?> GetByIdAsync(Guid participantSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync(
            "EXEC dbo.spgParticipantSchemeByParticipantSchemeId @ParticipantSchemeId",
            new { ParticipantSchemeId = participantSchemeId });

        if (row is null)
        {
            return null;
        }

        IDictionary<string, object> r = row;
        return new ParticipantSchemeRecord
        {
            ParticipantSchemeId = (Guid)r["fldParticipantSchemeId"],
            ContractId = (Guid)r["fldContractId"],
            ParticipantId = (Guid)r["fldParticipantId"],
            SchemeId = (Guid)r["fldSchemeId"],
            GroupAddressId = r["fldGroupAddressId"] as Guid?,
            DistributionMonthJan = GetBool(r, "fldJan"),
            DistributionMonthFeb = GetBool(r, "fldFeb"),
            DistributionMonthMar = GetBool(r, "fldMar"),
            DistributionMonthApr = GetBool(r, "fldApr"),
            DistributionMonthMay = GetBool(r, "fldMay"),
            DistributionMonthJun = GetBool(r, "fldJun"),
            DistributionMonthJul = GetBool(r, "fldJul"),
            DistributionMonthAug = GetBool(r, "fldAug"),
            DistributionMonthSep = GetBool(r, "fldSep"),
            DistributionMonthOct = GetBool(r, "fldOct"),
            DistributionMonthNov = GetBool(r, "fldNov"),
            DistributionMonthDec = GetBool(r, "fldDec"),
            CanEditJan = GetBool(r, "fldCanEditJan"),
            CanEditFeb = GetBool(r, "fldCanEditFeb"),
            CanEditMar = GetBool(r, "fldCanEditMar"),
            CanEditApr = GetBool(r, "fldCanEditApr"),
            CanEditMay = GetBool(r, "fldCanEditMay"),
            CanEditJun = GetBool(r, "fldCanEditJun"),
            CanEditJul = GetBool(r, "fldCanEditJul"),
            CanEditAug = GetBool(r, "fldCanEditAug"),
            CanEditSep = GetBool(r, "fldCanEditSep"),
            CanEditOct = GetBool(r, "fldCanEditOct"),
            CanEditNov = GetBool(r, "fldCanEditNov"),
            CanEditDec = GetBool(r, "fldCanEditDec"),
            NumberOfSetsRequired = GetInt(r, "fldNumberOfSetsRequired", 1),
            ExternalReference = r["fldExternalReference"] as string,
            Contact = r["fldContact"] as string,
            IsRemoved = GetBool(r, "fldIsRemoved"),
            ImportExportLicenceRequired = GetBool(r, "fldImportExportLicenceRequired"),
            CustomsCertificateRequired = GetBool(r, "fldCustomsCertificateRequired"),
            NonFeePaying = GetBool(r, "fldNonFeePaying"),
            PackingInstructions = r["fldPackingInstructions"] as string,
            IsWeightedPricing = GetBool(r, "fldIsWeightedPricing"),
            DataConsentDeclarationGiven = GetBool(r, "fldDataConsentDeclarationGiven"),
            IsOverrideJan = GetBool(r, "fldIsOverrideJan"),
            IsOverrideFeb = GetBool(r, "fldIsOverrideFeb"),
            IsOverrideMar = GetBool(r, "fldIsOverrideMar"),
            IsOverrideApr = GetBool(r, "fldIsOverrideApr"),
            IsOverrideMay = GetBool(r, "fldIsOverrideMay"),
            IsOverrideJun = GetBool(r, "fldIsOverrideJun"),
            IsOverrideJul = GetBool(r, "fldIsOverrideJul"),
            IsOverrideAug = GetBool(r, "fldIsOverrideAug"),
            IsOverrideSep = GetBool(r, "fldIsOverrideSep"),
            IsOverrideOct = GetBool(r, "fldIsOverrideOct"),
            IsOverrideNov = GetBool(r, "fldIsOverrideNov"),
            IsOverrideDec = GetBool(r, "fldIsOverrideDec"),
            Price = GetDecimal(r, "fldPrice"),
            ParticipantDisplayName = r["fldParticipantName"] as string ?? string.Empty,
            SchemeDisplayName = r["fldSchemeName"] as string ?? string.Empty
        };
    }

    // NULL bit columns (e.g. ImportExportLicenceRequired/CustomsCertificateRequired/IsWeightedPricing/
    // DataConsentDeclarationGiven on older rows) throw InvalidCastException on a direct (bool) cast -
    // Convert.ToBoolean(DBNull.Value) throws too, so this treats missing/null as false explicitly.
    private static bool GetBool(IDictionary<string, object> row, string column) =>
        row.TryGetValue(column, out var value) && value is bool flag && flag;

    // dbo.fnGetParticipantSchemePrice/fldNumberOfSetsRequired can be NULL for edge-case data (e.g.
    // missing pricing plan setup) - Convert.ToDecimal/ToInt32(DBNull.Value) throw InvalidCastException,
    // so these default to a safe value instead of failing the whole read.
    private static decimal GetDecimal(IDictionary<string, object> row, string column) =>
        row.TryGetValue(column, out var value) && value is not null && value is not DBNull ? Convert.ToDecimal(value) : 0m;

    private static int GetInt(IDictionary<string, object> row, string column, int defaultValue) =>
        row.TryGetValue(column, out var value) && value is not null && value is not DBNull ? Convert.ToInt32(value) : defaultValue;

    public async Task<ParticipantSchemeRecord> CreateAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(InsertSql, BuildParameters(record));
        var created = await GetByIdAsync(record.ParticipantSchemeId, cancellationToken);
        return created ?? throw new InvalidOperationException($"ParticipantScheme {record.ParticipantSchemeId} was inserted but could not be re-read.");
    }

    public async Task<bool> UpdateAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(UpdateSql, BuildParameters(record));
        return rowsAffected > 0;
    }

    private const string InsertSql =
        "EXEC dbo.spiParticipantScheme @ParticipantSchemeId, @ParticipantId, @SchemeId, @ContractId, @GroupAddressId, " +
        "@DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, " +
        "@DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, " +
        "@NumberOfSetsRequired, @ExternalReference, @Contact, @IsRemoved, @ImportExportLicenceRequired, @CustomsCertificateRequired, @NonFeePaying, " +
        "@PackingInstructions, @IsWeightedPricing, @DataConsentGiven, " +
        "@IsOverrideJan, @IsOverrideFeb, @IsOverrideMar, @IsOverrideApr, @IsOverrideMay, @IsOverrideJun, " +
        "@IsOverrideJul, @IsOverrideAug, @IsOverrideSep, @IsOverrideOct, @IsOverrideNov, @IsOverrideDec";

    private const string UpdateSql =
        "EXEC dbo.spuParticipantScheme @ParticipantSchemeId, @ParticipantId, @SchemeId, @ContractId, @GroupAddressId, " +
        "@DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, " +
        "@DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, " +
        "@NumberOfSetsRequired, @ExternalReference, @Contact, @IsRemoved, @ImportExportLicenceRequired, @CustomsCertificateRequired, @NonFeePaying, " +
        "@PackingInstructions, @IsWeightedPricing, @DataConsentGiven, " +
        "@IsOverrideJan, @IsOverrideFeb, @IsOverrideMar, @IsOverrideApr, @IsOverrideMay, @IsOverrideJun, " +
        "@IsOverrideJul, @IsOverrideAug, @IsOverrideSep, @IsOverrideOct, @IsOverrideNov, @IsOverrideDec";

    private static DynamicParameters BuildParameters(ParticipantSchemeRecord record)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@ParticipantSchemeId", record.ParticipantSchemeId);
        parameters.Add("@ParticipantId", record.ParticipantId);
        parameters.Add("@SchemeId", record.SchemeId);
        parameters.Add("@ContractId", record.ContractId);
        parameters.Add("@GroupAddressId", record.GroupAddressId);
        parameters.Add("@DistributionMonthJan", record.DistributionMonthJan);
        parameters.Add("@DistributionMonthFeb", record.DistributionMonthFeb);
        parameters.Add("@DistributionMonthMar", record.DistributionMonthMar);
        parameters.Add("@DistributionMonthApr", record.DistributionMonthApr);
        parameters.Add("@DistributionMonthMay", record.DistributionMonthMay);
        parameters.Add("@DistributionMonthJun", record.DistributionMonthJun);
        parameters.Add("@DistributionMonthJul", record.DistributionMonthJul);
        parameters.Add("@DistributionMonthAug", record.DistributionMonthAug);
        parameters.Add("@DistributionMonthSep", record.DistributionMonthSep);
        parameters.Add("@DistributionMonthOct", record.DistributionMonthOct);
        parameters.Add("@DistributionMonthNov", record.DistributionMonthNov);
        parameters.Add("@DistributionMonthDec", record.DistributionMonthDec);
        parameters.Add("@NumberOfSetsRequired", record.NumberOfSetsRequired);
        // fldExternalReference/fldContact/fldPackingInstructions are NOT NULL columns - legacy's
        // ParticipantScheme.vb private fields all default to String.Empty (never Nothing), so an
        // optional/blank field is stored as "" not NULL. Coalesce here rather than widening the
        // record/DTOs to allow a genuine NULL that the column would reject anyway.
        parameters.Add("@ExternalReference", record.ExternalReference ?? string.Empty);
        parameters.Add("@Contact", record.Contact ?? string.Empty);
        parameters.Add("@IsRemoved", record.IsRemoved);
        parameters.Add("@ImportExportLicenceRequired", record.ImportExportLicenceRequired);
        parameters.Add("@CustomsCertificateRequired", record.CustomsCertificateRequired);
        parameters.Add("@NonFeePaying", record.NonFeePaying);
        parameters.Add("@PackingInstructions", record.PackingInstructions ?? string.Empty);
        parameters.Add("@IsWeightedPricing", record.IsWeightedPricing);
        parameters.Add("@DataConsentGiven", record.DataConsentDeclarationGiven);
        parameters.Add("@IsOverrideJan", record.IsOverrideJan);
        parameters.Add("@IsOverrideFeb", record.IsOverrideFeb);
        parameters.Add("@IsOverrideMar", record.IsOverrideMar);
        parameters.Add("@IsOverrideApr", record.IsOverrideApr);
        parameters.Add("@IsOverrideMay", record.IsOverrideMay);
        parameters.Add("@IsOverrideJun", record.IsOverrideJun);
        parameters.Add("@IsOverrideJul", record.IsOverrideJul);
        parameters.Add("@IsOverrideAug", record.IsOverrideAug);
        parameters.Add("@IsOverrideSep", record.IsOverrideSep);
        parameters.Add("@IsOverrideOct", record.IsOverrideOct);
        parameters.Add("@IsOverrideNov", record.IsOverrideNov);
        parameters.Add("@IsOverrideDec", record.IsOverrideDec);
        return parameters;
    }
}
