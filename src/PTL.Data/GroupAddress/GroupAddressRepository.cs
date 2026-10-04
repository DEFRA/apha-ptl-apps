using System.Data;
using Dapper;
using PTL.Core.GroupAddress;
using PTL.Data.Infrastructure;
using CoreGroupAddress = PTL.Core.GroupAddress.GroupAddress;

namespace PTL.Data.GroupAddress;

public sealed class GroupAddressRepository(IDbConnectionFactory connectionFactory) : IGroupAddressRepository
{
    public async Task<IReadOnlyList<CoreGroupAddress>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<CoreGroupAddress>("EXEC dbo.spgaGroupAddress");
        return rows.ToList();
    }

    public async Task<CoreGroupAddress?> GetByIdAsync(Guid groupAddressId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CoreGroupAddress>(
            "EXEC dbo.spgGroupAddressByGroupAddressId @GroupAddressId",
            new { GroupAddressId = groupAddressId });
    }

    public async Task<CoreGroupAddress> CreateAsync(CoreGroupAddress groupAddress, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(InsertSql, BuildParameters(groupAddress));

        var created = await GetByIdAsync(groupAddress.GroupAddressId, cancellationToken);
        return created ?? throw new InvalidOperationException($"Group address {groupAddress.GroupAddressId} was inserted but could not be re-read.");
    }

    public async Task<CoreGroupAddress?> UpdateAsync(Guid groupAddressId, CoreGroupAddress groupAddress, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var rowsAffected = await connection.ExecuteAsync(UpdateSql, BuildParameters(groupAddress));
        return rowsAffected == 0 ? null : await GetByIdAsync(groupAddressId, cancellationToken);
    }

    private const string InsertSql =
        "EXEC dbo.spiGroupAddress @GroupAddressId, @Identifier, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @PackingInstructions";

    private const string UpdateSql =
        "EXEC dbo.spuGroupAddress @GroupAddressId, @Identifier, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @PackingInstructions";

    private static DynamicParameters BuildParameters(CoreGroupAddress groupAddress)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@GroupAddressId", groupAddress.GroupAddressId);
        parameters.Add("@Identifier", groupAddress.Identifier);
        parameters.Add("@Address1", groupAddress.Address1);
        parameters.Add("@Address2", groupAddress.Address2);
        parameters.Add("@Address3", groupAddress.Address3);
        parameters.Add("@Address4", groupAddress.Address4);
        parameters.Add("@Address5", groupAddress.Address5);
        parameters.Add("@CountryId", groupAddress.CountryId);
        parameters.Add("@Telephone", groupAddress.Telephone);
        parameters.Add("@PackingInstructions", groupAddress.PackingInstructions);
        return parameters;
    }
}
