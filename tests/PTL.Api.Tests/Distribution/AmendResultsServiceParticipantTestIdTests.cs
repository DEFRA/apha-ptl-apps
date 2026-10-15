using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.AmendResultsTestData;

namespace PTL.Api.Tests.Distribution;

// AmendResults.cshtml only draws a participant's method/result/category input when the value's TestId
// matches the test table being drawn, so the service must fill TestId in.
public class AmendResultsServiceParticipantTestIdTests
{
    [Fact]
    public async Task GetAsync_ParticipantMethodResultAndCategoryValues_CarryTheTestTheyBelongTo()
    {
        var repository = new FakeDistributionRepository();
        repository.AmendResults[SchemeId] = Source();

        var lab1 = (await new AmendResultsService(repository).GetAsync(SchemeId))!.Participants[0];

        Assert.All(lab1.MethodResults, m => Assert.Equal(Culture, m.TestId));
        Assert.All(lab1.ResultItemResults, r => Assert.Equal(Culture, r.TestId));
        Assert.All(lab1.CategoryResults, c => Assert.Equal(Culture, c.TestId));
    }
}
