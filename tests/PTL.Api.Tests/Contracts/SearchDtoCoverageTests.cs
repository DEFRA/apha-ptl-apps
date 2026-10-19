using PTL.Contracts.Search;

namespace PTL.Api.Tests.Contracts;

// Coverlet only counts a record property as covered when its getter actually runs - see SharedDtoCoverageTests.
public class SearchDtoCoverageTests
{
    [Fact]
    public void CustomerSearchCriteriaRequest_ExposesEveryProperty_AndDefaultsToNoFilter()
    {
        var countryId = Guid.NewGuid();

        var request = new CustomerSearchCriteriaRequest("QAL/1", "ACC-1", "Alpha", "Amy", "Org", countryId);

        Assert.Equal("QAL/1", request.QalNumber);
        Assert.Equal("ACC-1", request.AccountNumber);
        Assert.Equal("Alpha", request.Name);
        Assert.Equal("Amy", request.ContactName);
        Assert.Equal("Org", request.Organisation);
        Assert.Equal(countryId, request.CountryId);

        var defaults = new CustomerSearchCriteriaRequest();

        Assert.Null(defaults.QalNumber);
        Assert.Null(defaults.AccountNumber);
        Assert.Null(defaults.Name);
        Assert.Null(defaults.ContactName);
        Assert.Null(defaults.Organisation);
        Assert.Null(defaults.CountryId);
    }

    [Fact]
    public void CustomerSearchResultResponse_ExposesEveryProperty()
    {
        var customerId = Guid.NewGuid();

        var response = new CustomerSearchResultResponse(customerId, "QAL/1", "ACC-1", "Alpha", "Amy", "Org", "France");

        Assert.Equal(customerId, response.CustomerId);
        Assert.Equal("QAL/1", response.QalNumber);
        Assert.Equal("ACC-1", response.AccountNumber);
        Assert.Equal("Alpha", response.Name);
        Assert.Equal("Amy", response.ContactName);
        Assert.Equal("Org", response.Organisation);
        Assert.Equal("France", response.Country);
    }

    [Fact]
    public void ParticipantSearchCriteriaRequest_ExposesEveryProperty_AndDefaultsToNoFilter()
    {
        var request = new ParticipantSearchCriteriaRequest("LAB1", "Lab One", "Bob");

        Assert.Equal("LAB1", request.LabCode);
        Assert.Equal("Lab One", request.LabName);
        Assert.Equal("Bob", request.ContactName);

        var defaults = new ParticipantSearchCriteriaRequest();

        Assert.Null(defaults.LabCode);
        Assert.Null(defaults.LabName);
        Assert.Null(defaults.ContactName);
    }

    [Fact]
    public void ParticipantSearchResultResponse_ExposesEveryProperty()
    {
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var response = new ParticipantSearchResultResponse(participantId, customerId, "LAB1", "OLD1", "Lab One", "Bob");

        Assert.Equal(participantId, response.ParticipantId);
        Assert.Equal(customerId, response.CustomerId);
        Assert.Equal("LAB1", response.LabCode);
        Assert.Equal("OLD1", response.OldLabCode);
        Assert.Equal("Lab One", response.LabName);
        Assert.Equal("Bob", response.ContactName);
    }
}
