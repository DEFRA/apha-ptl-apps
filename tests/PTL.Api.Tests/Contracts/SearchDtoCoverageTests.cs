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

    [Fact]
    public void DistributionNumberSearchCriteriaRequest_ExposesTheProperty_AndDefaultsToNoFilter()
    {
        var request = new DistributionNumberSearchCriteriaRequest("D1a");

        Assert.Equal("D1a", request.DistributionNumber);
        Assert.Null(new DistributionNumberSearchCriteriaRequest().DistributionNumber);
    }

    [Fact]
    public void DistributionNumberSearchResultResponse_ExposesEveryProperty()
    {
        var monthlyDistributionSchemeId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();

        var response = new DistributionNumberSearchResultResponse(monthlyDistributionSchemeId, schemeId, "D1a/00001", "D1a/S1 BT", "D1a", "2026", "May");

        Assert.Equal(monthlyDistributionSchemeId, response.MonthlyDistributionSchemeId);
        Assert.Equal(schemeId, response.SchemeId);
        Assert.Equal("D1a/00001", response.DistributionNumber);
        Assert.Equal("D1a/S1 BT", response.SchemeName);
        Assert.Equal("D1a", response.SchemeIdentifier);
        Assert.Equal("2026", response.Year);
        Assert.Equal("May", response.Month);
    }

    [Fact]
    public void SampleNumberSearchCriteriaRequest_ExposesTheProperty_AndDefaultsToNoFilter()
    {
        var request = new SampleNumberSearchCriteriaRequest("S00001");

        Assert.Equal("S00001", request.SampleNumber);
        Assert.Null(new SampleNumberSearchCriteriaRequest().SampleNumber);
    }

    [Fact]
    public void SampleNumberSearchResultResponse_ExposesEveryProperty()
    {
        var sampleId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();

        var response = new SampleNumberSearchResultResponse(sampleId, schemeId, "S00001", "D1a/S1 BT", "D1a", "D1a/00001", "2026", "May");

        Assert.Equal(sampleId, response.SampleId);
        Assert.Equal(schemeId, response.SchemeId);
        Assert.Equal("S00001", response.SampleNumber);
        Assert.Equal("D1a/S1 BT", response.SchemeName);
        Assert.Equal("D1a", response.SchemeIdentifier);
        Assert.Equal("D1a/00001", response.DistributionNumber);
        Assert.Equal("2026", response.Year);
        Assert.Equal("May", response.Month);
    }
}
