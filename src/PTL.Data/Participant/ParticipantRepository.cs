using System.Data;
using Dapper;
using PTL.Core.Participant;
using PTL.Data.Infrastructure;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Data.Participant;

public sealed class ParticipantRepository(IDbConnectionFactory connectionFactory) : IParticipantRepository
{
    public async Task<CoreParticipant?> GetByIdAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<CoreParticipant>(
            "EXEC dbo.spgParticipantByParticipantId @ParticipantId",
            new { ParticipantId = participantId });
    }

    public async Task<CoreParticipant?> GetBySsoIdAsync(Guid ssoId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<CoreParticipant>(
            "EXEC dbo.spgParticipantBySsoId @SsoId",
            new { SsoId = ssoId });
    }

    public async Task<IReadOnlyList<ParticipantSummaryEntity>> GetSummariesAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<ParticipantSummaryEntity>(
            "EXEC dbo.spgParticipantInfoByCustomerId @CustomerId, @ActiveOnly",
            new { CustomerId = customerId, ActiveOnly = !includeInactive })).ToList();
    }

    public async Task<CoreParticipant> CreateAsync(CoreParticipant participant, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(InsertSql, BuildParameters(participant));
        var created = await GetByIdAsync(participant.ParticipantId, cancellationToken);
        return created ?? throw new InvalidOperationException($"Participant {participant.ParticipantId} was inserted but could not be re-read.");
    }

    public async Task<CoreParticipant?> UpdateAsync(CoreParticipant participant, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(UpdateSql, BuildParameters(participant));
        return rowsAffected == 0 ? null : await GetByIdAsync(participant.ParticipantId, cancellationToken);
    }

    private const string InsertSql =
        "EXEC dbo.spiParticipant @ParticipantId, @SsoId, @CustomerId, @LabCode, @LabName, @LabTypeId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Fax, @Email, @Email2, @Comments, @IsActive, @InactiveDate, @InactiveError, @InactiveErrorDate";

    private const string UpdateSql =
        "EXEC dbo.spuParticipant @ParticipantId, @SsoId, @CustomerId, @LabCode, @LabName, @LabTypeId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Fax, @Email, @Email2, @Comments, @IsActive, @InactiveDate, @InactiveError, @InactiveErrorDate";

    private static DynamicParameters BuildParameters(CoreParticipant participant)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@ParticipantId", participant.ParticipantId);
        parameters.Add("@SsoId", participant.SsoId);
        parameters.Add("@CustomerId", participant.CustomerId);
        parameters.Add("@LabCode", participant.LabCode);
        parameters.Add("@LabName", participant.LabName);
        parameters.Add("@LabTypeId", participant.LabTypeId);
        parameters.Add("@ContactName", participant.ContactName);
        parameters.Add("@Organisation", participant.Organisation);
        parameters.Add("@Address1", participant.Address1);
        parameters.Add("@Address2", participant.Address2);
        parameters.Add("@Address3", participant.Address3);
        parameters.Add("@Address4", participant.Address4);
        parameters.Add("@Address5", participant.Address5);
        parameters.Add("@CountryId", participant.CountryId);
        parameters.Add("@Telephone", participant.Telephone);
        parameters.Add("@Fax", participant.Fax);
        parameters.Add("@Email", participant.Email);
        parameters.Add("@Email2", participant.Email2);
        parameters.Add("@Comments", participant.Comments);
        parameters.Add("@IsActive", participant.IsActive);
        parameters.Add("@InactiveDate", participant.InactiveDate);
        parameters.Add("@InactiveError", participant.InactiveError);
        parameters.Add("@InactiveErrorDate", participant.InactiveErrorDate);
        return parameters;
    }
}
