using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Tests.TestSupport;

internal static class TestConsultantTestData
{
    public static readonly Guid SchemeId = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    public static TabulationTableResponse Table(string caption, params string[] headings) => new(
        caption,
        [
            new TabulationGridRowResponse([.. headings.Select(h => new TabulationCellResponse(h, 1, 1, "Header1", false))], true),
            new TabulationGridRowResponse([.. headings.Select(h => new TabulationCellResponse("r-" + h, 1, 1, "Cell", false))], false),
        ],
        null);

    public static TestConsultantTabulationResponse Tabulation(
        bool submitted = false,
        string heading = "Add your comments here:",
        string commentsHtml = "<p>Draft</p>",
        bool hasMethodItems = true,
        string[]? samples = null,
        bool withSampleInformation = true,
        bool withRatings = false)
    {
        samples ??= ["26/1", "26/2"];
        return new TestConsultantTabulationResponse(
            SchemeId,
            "FINAL REPORT for D1a/S1 BT: Brucella",
            "Date Of Distribution: 03/05/2026",
            "<p>Standard text</p>",
            "<p>Scheme comments</p>",
            hasMethodItems,
            samples,
            [Table("PCR results", "PCR", "Method Details")],
            withRatings ? Table("QAU Ratings", "QAU Ratings") : null,
            [Table("PCR full", "PCR", "Full Method")],
            withRatings ? Table("Full Ratings", "Full Ratings") : null,
            Table("Participant information", "Participant"),
            withSampleInformation ? [new TestConsultantSampleInformationResponse("26/1", "Positive", "Details of sample one")] : [],
            new TestConsultantCommentsStateResponse(submitted, heading, commentsHtml, commentsHtml.Length > 0));
    }

    public static TestConsultantDistributionResponse Dist(
        int year, int month, string title, bool submitted = false, Guid? schemeId = null) =>
        new(schemeId ?? Guid.NewGuid(), year, month, title, new DateTime(year, month, 21), submitted, submitted ? "Comments submitted" : "Awaiting comments");
}
