using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PTL.Contracts.Distribution;

namespace PTL.ExternalWeb.Features.Comments;

public sealed class CommentsEntryViewModel
{
    [BindRequired]
    public Guid MonthlyDistributionSchemeId { get; set; }

    public string Title { get; set; } = string.Empty;

    public TestConsultantCommentsStateResponse State { get; set; } = new(false, string.Empty, string.Empty, false);

    public string StandardTabulationTextHtml { get; set; } = string.Empty;

    public string ExistingCommentsHtml { get; set; } = string.Empty;

    public IReadOnlyList<TabulationTableResponse> ResultsTables { get; set; } = [];

    public TabulationTableResponse? ResultsRatings { get; set; }

    public IReadOnlyList<TabulationTableResponse> FullTables { get; set; } = [];

    public TabulationTableResponse? FullRatings { get; set; }

    public TabulationTableResponse? ParticipantInformation { get; set; }

    public IReadOnlyList<TestConsultantSampleInformationResponse> SampleInformation { get; set; } = [];

    [StringLength(50000, ErrorMessage = "Comments must not exceed 50,000 characters. Note that rich text such as font changes and bullet points will artificially count towards this character limit.")]
    public string? Comments { get; set; }

    [BindRequired]
    public bool Acknowledged { get; set; }

    public bool IsSubmitted => State.IsSubmitted;

    public static CommentsEntryViewModel From(Guid schemeId, TestConsultantTabulationResponse tabulation, string? commentsOverride = null) =>
        new()
        {
            MonthlyDistributionSchemeId = schemeId,
            Title = tabulation.Title,
            State = tabulation.Comments,
            StandardTabulationTextHtml = tabulation.StandardTabulationTextHtml,
            ExistingCommentsHtml = tabulation.CommentsHtml,
            ResultsTables = tabulation.ResultsTables,
            ResultsRatings = tabulation.ResultsRatings,
            FullTables = tabulation.FullTables,
            FullRatings = tabulation.FullRatings,
            ParticipantInformation = tabulation.ParticipantInformation,
            SampleInformation = tabulation.SampleInformation,
            Comments = commentsOverride ?? tabulation.Comments.CommentsHtml,
        };
}
