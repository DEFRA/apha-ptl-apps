using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PTL.Core.Participant;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Data.Participant;

public sealed class ParticipantRepository(PtlDbContext dbContext) : IParticipantRepository
{
    public async Task<CoreParticipant?> GetByIdAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        // EXEC ... is not composable SQL, so SingleOrDefaultAsync (which wraps the query) cannot be used here.
        var parameter = new SqlParameter("@ParticipantId", participantId);
        var results = await dbContext.Participants
            .FromSqlRaw("EXEC dbo.spgParticipantByParticipantId @ParticipantId", parameter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return results.SingleOrDefault();
    }

    public async Task<CoreParticipant?> GetBySsoIdAsync(Guid ssoId, CancellationToken cancellationToken = default)
    {
        var parameter = new SqlParameter("@SsoId", ssoId);
        var results = await dbContext.Participants
            .FromSqlRaw("EXEC dbo.spgParticipantBySsoId @SsoId", parameter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return results.SingleOrDefault();
    }

    public async Task<IReadOnlyList<ParticipantSummaryEntity>> GetSummariesAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var activeOnly = new SqlParameter("@ActiveOnly", SqlDbType.Bit)
        {
            Value = includeInactive ? false : true
        };

        return await dbContext.ParticipantSummaries
            .FromSqlRaw("EXEC dbo.spgParticipantInfoByCustomerId @CustomerId, @ActiveOnly",
                new SqlParameter("@CustomerId", customerId),
                activeOnly)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<CoreParticipant> CreateAsync(CoreParticipant participant, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync(InsertSql, BuildParameters(participant), cancellationToken);
        var created = await GetByIdAsync(participant.ParticipantId, cancellationToken);
        return created ?? throw new InvalidOperationException($"Participant {participant.ParticipantId} was inserted but could not be re-read.");
    }

    public async Task<CoreParticipant?> UpdateAsync(CoreParticipant participant, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(UpdateSql, BuildParameters(participant), cancellationToken);
        return rowsAffected == 0 ? null : await GetByIdAsync(participant.ParticipantId, cancellationToken);
    }

    private const string InsertSql =
        "EXEC dbo.spiParticipant @ParticipantId, @SsoId, @CustomerId, @LabCode, @LabName, @LabTypeId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Fax, @Email, @Email2, @Comments, @IsActive, @InactiveDate, @InactiveError, @InactiveErrorDate";

    private const string UpdateSql =
        "EXEC dbo.spuParticipant @ParticipantId, @SsoId, @CustomerId, @LabCode, @LabName, @LabTypeId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Fax, @Email, @Email2, @Comments, @IsActive, @InactiveDate, @InactiveError, @InactiveErrorDate";

    private static SqlParameter[] BuildParameters(CoreParticipant participant) =>
    [
        new("@ParticipantId", participant.ParticipantId),
        new("@SsoId", participant.SsoId),
        new("@CustomerId", participant.CustomerId),
        new("@LabCode", participant.LabCode),
        new("@LabName", participant.LabName),
        new("@LabTypeId", participant.LabTypeId),
        new("@ContactName", participant.ContactName),
        new("@Organisation", participant.Organisation),
        new("@Address1", participant.Address1),
        new("@Address2", participant.Address2),
        new("@Address3", participant.Address3),
        new("@Address4", participant.Address4),
        new("@Address5", participant.Address5),
        new("@CountryId", participant.CountryId),
        new("@Telephone", participant.Telephone),
        new("@Fax", participant.Fax),
        new("@Email", participant.Email),
        new("@Email2", participant.Email2),
        new("@Comments", participant.Comments),
        new("@IsActive", participant.IsActive),
        new("@InactiveDate", (object?)participant.InactiveDate ?? DBNull.Value),
        new("@InactiveError", participant.InactiveError),
        new("@InactiveErrorDate", (object?)participant.InactiveErrorDate ?? DBNull.Value)
    ];
}
