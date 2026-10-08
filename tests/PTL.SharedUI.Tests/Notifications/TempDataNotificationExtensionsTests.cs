using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.SharedUI.Notifications;
using PTL.SharedUI.Tests.TestSupport;

namespace PTL.SharedUI.Tests.Notifications;

public class TempDataNotificationExtensionsTests
{
    private static TempDataDictionary CreateTempData() =>
        new(new DefaultHttpContext(), new FakeTempDataProvider());

    [Fact]
    public void SetNotification_ThenGetNotification_RoundTrips()
    {
        var tempData = CreateTempData();

        tempData.SetNotification(NotificationType.Success, "Saved successfully.");
        var notification = tempData.GetNotification();

        Assert.NotNull(notification);
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Saved successfully.", notification.Message);
    }

    [Fact]
    public void SetNotification_EmptyMessage_Throws()
    {
        var tempData = CreateTempData();

        Assert.Throws<ArgumentException>(() => tempData.SetNotification(NotificationType.Error, string.Empty));
    }

    [Fact]
    public void GetNotification_WhenNoneSet_ReturnsNull()
    {
        var tempData = CreateTempData();

        Assert.Null(tempData.GetNotification());
    }

    [Fact]
    public void GetNotification_RemovesValueAfterReading()
    {
        var tempData = CreateTempData();
        tempData.SetNotification(NotificationType.Error, "Something failed.");

        tempData.GetNotification();
        var second = tempData.GetNotification();

        Assert.Null(second);
    }

    [Fact]
    public void SetNotification_NullTempData_DoesNotThrow()
    {
        ITempDataDictionary? tempData = null;

        var exception = Record.Exception(() => tempData!.SetNotification(NotificationType.Success, "message"));

        Assert.Null(exception);
    }

    [Fact]
    public void GetNotification_NullTempData_ReturnsNull()
    {
        ITempDataDictionary? tempData = null;

        Assert.Null(tempData!.GetNotification());
    }

    [Fact]
    public void GetNotification_NonStringPayload_ReturnsNull()
    {
        var tempData = CreateTempData();
        tempData["PTL.Notification"] = 42;

        Assert.Null(tempData.GetNotification());
    }

    [Fact]
    public void GetNotification_MalformedJsonPayload_ReturnsNull()
    {
        var tempData = CreateTempData();
        tempData["PTL.Notification"] = "{not-json";

        Assert.Null(tempData.GetNotification());
    }
}
