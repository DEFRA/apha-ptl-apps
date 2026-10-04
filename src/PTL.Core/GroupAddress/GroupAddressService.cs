using Microsoft.Extensions.Logging;

namespace PTL.Core.GroupAddress;

public sealed class GroupAddressService(IGroupAddressRepository groupAddressRepository) : IGroupAddressService
{
    public Task<IReadOnlyList<GroupAddress>> GetAllAsync(CancellationToken cancellationToken = default) =>
        groupAddressRepository.GetAllAsync(cancellationToken);

    public async Task<GroupAddressSearchResult> SearchAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var all = (await groupAddressRepository.GetAllAsync(cancellationToken))
            .OrderBy(g => g.Identifier)
            .ToList();

        var totalCount = all.Count;
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new GroupAddressSearchResult(items, totalCount);
    }

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

    private static void Validate(GroupAddress groupAddress)
    {
        var result = GroupAddressValidator.Validate(groupAddress);
        if (!result.IsValid)
        {
            throw new GroupAddressValidationException(result.Errors);
        }
    }
}
