using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace PTL.InternalWeb.Notifications;

public static class TempDataNotificationExtensions
{
    private const string NotificationTempDataKey = "PTL.Notification";

    public static void SetNotification(this ITempDataDictionary tempData, NotificationType type, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Notification message must be provided.", nameof(message));
        }

        if (tempData is null)
        {
            return;
        }

        tempData[NotificationTempDataKey] = JsonSerializer.Serialize(new NotificationMessage(type, message));
    }

    public static NotificationMessage? GetNotification(this ITempDataDictionary tempData)
    {
        if (tempData is null)
        {
            return null;
        }

        if (!tempData.TryGetValue(NotificationTempDataKey, out var value) || value is not string payload)
        {
            return null;
        }

        tempData.Remove(NotificationTempDataKey);

        try
        {
            return JsonSerializer.Deserialize<NotificationMessage>(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
