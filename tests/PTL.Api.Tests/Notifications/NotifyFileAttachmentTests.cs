using System.Text;
using PTL.Core.Notifications;

namespace PTL.Api.Tests.Notifications;

public class NotifyFileAttachmentTests
{
    [Fact]
    public void Build_Base64EncodesContentAndKeepsFilename()
    {
        var content = Encoding.UTF8.GetBytes("invoice-pdf-bytes");

        var value = NotifyFileAttachment.Build(content, "invoice.pdf");

        Assert.Equal(Convert.ToBase64String(content), value.file);
        Assert.Equal("invoice.pdf", value.filename);
    }

    // Notify would otherwise prompt the recipient for an email address before releasing the file.
    [Fact]
    public void Build_DoesNotRequireEmailConfirmationAndSetsNoRetentionOverride()
    {
        var value = NotifyFileAttachment.Build([1, 2, 3], "invoice.pdf");

        Assert.False(value.confirm_email_before_download);
        Assert.Null(value.retention_period);
    }

    [Fact]
    public void Build_EmptyContent_ProducesEmptyBase64()
    {
        var value = NotifyFileAttachment.Build([], "empty.pdf");

        Assert.Equal(string.Empty, value.file);
    }

    [Fact]
    public void MaxFileSizeBytes_MatchesNotifyDocumentedLimit()
    {
        Assert.Equal(2 * 1024 * 1024, NotifyFileAttachment.MaxFileSizeBytes);
    }
}
