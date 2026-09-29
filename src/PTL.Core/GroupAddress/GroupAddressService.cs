using Microsoft.Extensions.Logging;

namespace PTL.Core.GroupAddress;

public sealed class GroupAddressService(IGroupAddressRepository groupAddressRepository, ILogger<GroupAddressService> logger) : IGroupAddressService
{
    public Task<IReadOnlyList<GroupAddress>> GetAllAsync(CancellationToken cancellationToken = default) =>
        groupAddressRepository.GetAllAsync(cancellationToken);

    public Task<GroupAddress?> GetByIdAsync(Guid groupAddressId, CancellationToken cancellationToken = default) =>
        groupAddressRepository.GetByIdAsync(groupAddressId, cancellationToken);

    public async Task<GroupAddress> CreateAsync(GroupAddress groupAddress, CancellationToken cancellationToken = default)
    {
        groupAddress.GroupAddressId = Guid.NewGuid();
        Validate(groupAddress);

        var created = await groupAddressRepository.CreateAsync(groupAddress, cancellationToken);
        return created;
    }

    public async Task<GroupAddress?> UpdateAsync(Guid groupAddressId, GroupAddress groupAddress, CancellationToken cancellationToken = default)
    {
        var existing = await groupAddressRepository.GetByIdAsync(groupAddressId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        groupAddress.GroupAddressId = existing.GroupAddressId;
        Validate(groupAddress);

        return await groupAddressRepository.UpdateAsync(groupAddressId, groupAddress, cancellationToken);
    }

    private void Validate(GroupAddress groupAddress)
    {
        var result = GroupAddressValidator.Validate(groupAddress);
        if (!result.IsValid)
        {
            throw new GroupAddressValidationException(result.Errors);
        }
    }
}
