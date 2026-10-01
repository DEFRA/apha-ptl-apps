using PTL.Contracts.GroupAddress;

namespace PTL.Api.Tests.GroupAddress;

public class GroupAddressEntityAndDtoCoverageTests
{
    [Fact]
    public void GroupAddress_ExposesAllProperties()
    {
        var groupAddressId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var groupAddress = new PTL.Core.GroupAddress.GroupAddress
        {
            GroupAddressId = groupAddressId,
            Identifier = "PTL-001",
            Address1 = "1 Sample Street",
            Address2 = "Second Line",
            Address3 = "Third Line",
            Address4 = "Fourth Line",
            Address5 = "Fifth Line",
            CountryId = countryId,
            Telephone = "020 1234 5678",
            PackingInstructions = "Fragile"
        };

        Assert.Equal(groupAddressId, groupAddress.GroupAddressId);
        Assert.Equal("PTL-001", groupAddress.Identifier);
        Assert.Equal("1 Sample Street", groupAddress.Address1);
        Assert.Equal("Second Line", groupAddress.Address2);
        Assert.Equal("Third Line", groupAddress.Address3);
        Assert.Equal("Fourth Line", groupAddress.Address4);
        Assert.Equal("Fifth Line", groupAddress.Address5);
        Assert.Equal(countryId, groupAddress.CountryId);
        Assert.Equal("020 1234 5678", groupAddress.Telephone);
        Assert.Equal("Fragile", groupAddress.PackingInstructions);
    }

    [Fact]
    public void GroupAddressValidationException_ExposesErrors()
    {
        var error = new PTL.Core.GroupAddress.GroupAddressValidationError("Identifier", "Identifier is required");
        var exception = new PTL.Core.GroupAddress.GroupAddressValidationException([error]);

        Assert.Single(exception.Errors);
        Assert.Equal("Identifier", exception.Errors[0].Field);
        Assert.Equal("Identifier is required", exception.Errors[0].Message);
        Assert.Equal("Group Address validation failed.", exception.Message);
    }

    [Fact]
    public void GroupAddressResponse_ExposesAllProperties()
    {
        var groupAddressId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var response = new GroupAddressResponse(groupAddressId, "PTL-001", "1 Sample Street", "Second Line", "Third Line", "Fourth Line", "Fifth Line", countryId, "020 1234 5678", "Fragile");

        Assert.Equal(groupAddressId, response.GroupAddressId);
        Assert.Equal("PTL-001", response.Identifier);
        Assert.Equal("1 Sample Street", response.Address1);
        Assert.Equal("Second Line", response.Address2);
        Assert.Equal("Third Line", response.Address3);
        Assert.Equal("Fourth Line", response.Address4);
        Assert.Equal("Fifth Line", response.Address5);
        Assert.Equal(countryId, response.CountryId);
        Assert.Equal("020 1234 5678", response.Telephone);
        Assert.Equal("Fragile", response.PackingInstructions);
    }

    [Fact]
    public void GroupAddressSaveRequest_ExposesAllProperties()
    {
        var countryId = Guid.NewGuid();
        var request = new GroupAddressSaveRequest("PTL-001", "1 Sample Street", "Second Line", "Third Line", "Fourth Line", "Fifth Line", countryId, "020 1234 5678", "Fragile");

        Assert.Equal("PTL-001", request.Identifier);
        Assert.Equal("1 Sample Street", request.Address1);
        Assert.Equal("Second Line", request.Address2);
        Assert.Equal("Third Line", request.Address3);
        Assert.Equal("Fourth Line", request.Address4);
        Assert.Equal("Fifth Line", request.Address5);
        Assert.Equal(countryId, request.CountryId);
        Assert.Equal("020 1234 5678", request.Telephone);
        Assert.Equal("Fragile", request.PackingInstructions);
    }

    [Fact]
    public void GroupAddressSearchRequest_DefaultsAndOverrides()
    {
        var defaults = new GroupAddressSearchRequest();
        var overridden = new GroupAddressSearchRequest(2, 50);

        Assert.Equal(1, defaults.Page);
        Assert.Equal(20, defaults.PageSize);
        Assert.Equal(2, overridden.Page);
        Assert.Equal(50, overridden.PageSize);
    }

    [Fact]
    public void GroupAddressSearchResponse_ExposesAllProperties()
    {
        var response = new GroupAddressResponse(Guid.NewGuid(), "PTL-001", "1 Sample Street", string.Empty, string.Empty, string.Empty, string.Empty, Guid.NewGuid(), string.Empty, string.Empty);
        var search = new GroupAddressSearchResponse([response], 1, 1, 20);

        Assert.Single(search.Items);
        Assert.Equal(1, search.TotalCount);
        Assert.Equal(1, search.Page);
        Assert.Equal(20, search.PageSize);
    }

    [Fact]
    public void LookupGroupAddressResponse_ExposesAllProperties()
    {
        var groupAddressId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var response = new PTL.Contracts.Lookup.GroupAddressResponse(groupAddressId, "PTL-001", "1 Sample Street", countryId);

        Assert.Equal(groupAddressId, response.GroupAddressId);
        Assert.Equal("PTL-001", response.Identifier);
        Assert.Equal("1 Sample Street", response.Address1);
        Assert.Equal(countryId, response.CountryId);
    }

    [Fact]
    public void LookupGroupAddressEntity_ExposesAllProperties()
    {
        var groupAddressId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var entity = new PTL.Core.Lookup.GroupAddressEntity { GroupAddressId = groupAddressId, Identifier = "PTL-001", Address1 = "1 Sample Street", CountryId = countryId };

        Assert.Equal(groupAddressId, entity.GroupAddressId);
        Assert.Equal("PTL-001", entity.Identifier);
        Assert.Equal("1 Sample Street", entity.Address1);
        Assert.Equal(countryId, entity.CountryId);
    }
}
