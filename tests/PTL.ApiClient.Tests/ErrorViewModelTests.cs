namespace PTL.ApiClient.Tests;

public class ErrorViewModelTests
{
    [Fact]
    public void ShowRequestId_WithRequestId_ReturnsTrue()
    {
        var model = new ErrorViewModel { RequestId = "abc123" };

        Assert.True(model.ShowRequestId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ShowRequestId_WithNoRequestId_ReturnsFalse(string? requestId)
    {
        var model = new ErrorViewModel { RequestId = requestId };

        Assert.False(model.ShowRequestId);
    }
}
