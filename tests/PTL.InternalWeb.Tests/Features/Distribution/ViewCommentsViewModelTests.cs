using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;

namespace PTL.InternalWeb.Tests.Features.Distribution;

public class ViewCommentsViewModelTests
{
    private static readonly Guid SavedA = Guid.Parse("cccccccc-0000-0000-0000-0000000000c1");
    private static readonly Guid SavedB = Guid.Parse("cccccccc-0000-0000-0000-0000000000c2");
    private static readonly Guid Token = Guid.Parse("cccccccc-0000-0000-0000-0000000000c3");

    [Fact]
    public void Defaults_HaveNothingStagedOrRemoved()
    {
        var model = new ViewCommentsViewModel();

        Assert.Empty(model.StagedAttachments);
        Assert.Empty(model.RemovedAttachmentIds);
        Assert.Empty(model.SavedAttachments);
        Assert.Empty(model.StagedFiles);
        Assert.Empty(model.Attachments);
        Assert.Equal(string.Empty, model.DistributionTitle);
    }

    [Fact]
    public void StagedFiles_ParsesTokenAndFileName_AndIgnoresMalformedValues()
    {
        var model = new ViewCommentsViewModel
        {
            StagedAttachments = [$"{Token}|report|v2.pdf", "not-a-guid|bad.pdf", "no-separator", $"{SavedA}"],
        };

        Assert.Equal([(Token, "report|v2.pdf")], model.StagedFiles);
    }

    [Fact]
    public void Attachments_AreTheSavedOnesNotRemoved_ThenTheStagedOnes()
    {
        var model = new ViewCommentsViewModel
        {
            SavedAttachments = [new CommentsAttachmentResponse(SavedA, "a.pdf"), new CommentsAttachmentResponse(SavedB, "b.pdf")],
            RemovedAttachmentIds = [SavedA],
            StagedAttachments = [$"{Token}|new.pdf"],
        };

        Assert.Equal(
            [new ViewCommentsAttachment(SavedB, "b.pdf", false), new ViewCommentsAttachment(Token, "new.pdf", true)],
            model.Attachments);
    }
}
