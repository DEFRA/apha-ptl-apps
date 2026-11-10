using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PTL.Contracts.Distribution;

namespace PTL.ExternalWeb.Features.Comments;

public sealed class CommentsEntryViewModel
{
    [BindRequired]
    public Guid MonthlyDistributionSchemeId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public TestConsultantCommentsStateResponse State { get; set; } = new(false, string.Empty, string.Empty, false);

    public string StandardTabulationTextHtml { get; set; } = string.Empty;

    public string ExistingCommentsHtml { get; set; } = string.Empty;

    public IReadOnlyList<TabulationTableResponse> ResultsTables { get; set; } = [];

    public TabulationTableResponse? ResultsRatings { get; set; }

    public IReadOnlyList<TabulationTableResponse> FullTables { get; set; } = [];

    public TabulationTableResponse? FullRatings { get; set; }

    public TabulationTableResponse? ParticipantInformation { get; set; }

    public IReadOnlyList<TestConsultantSampleInformationResponse> SampleInformation { get; set; } = [];

    public List<string> AllSamples { get; set; } = [];

    public List<string> ShownSamples { get; set; } = [];

    public bool ShowMethodItems { get; set; } = true;

    public bool? IsFiltered { get; set; }

    [StringLength(50000, ErrorMessage = "Comments must not exceed 50,000 characters. Note that rich text such as font changes and bullet points will artificially count towards this character limit.")]
    public string? Comments { get; set; }

    [BindRequired]
    public bool Acknowledged { get; set; }

    public bool IsSubmitted => State.IsSubmitted;

    public TestConsultantTabulationFilter ToFilter() =>
        IsFiltered == true
            ? new TestConsultantTabulationFilter([.. AllSamples.Except(ShownSamples, StringComparer.Ordinal)], !ShowMethodItems)
            : TestConsultantTabulationFilter.None;

    public static CommentsEntryViewModel From(
        Guid schemeId,
        TestConsultantTabulationResponse tabulation,
        string? commentsOverride = null,
        CommentsEntryViewModel? previous = null)
    {
        var model = new CommentsEntryViewModel
        {
            MonthlyDistributionSchemeId = schemeId,
            Title = tabulation.Title,
            ReturnUrl = previous?.ReturnUrl,
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
            IsFiltered = previous?.IsFiltered ?? true,
            ShowMethodItems = previous?.ShowMethodItems ?? true,
            Acknowledged = previous?.Acknowledged ?? false,
            AllSamples = [.. tabulation.SampleNumbers],
        };

        model.ShownSamples = previous?.IsFiltered == true
            ? [.. tabulation.SampleNumbers.Intersect(previous.ShownSamples, StringComparer.Ordinal)]
            : [.. tabulation.SampleNumbers];

        return model;
    }
}
