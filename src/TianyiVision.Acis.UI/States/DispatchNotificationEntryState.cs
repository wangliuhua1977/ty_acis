namespace TianyiVision.Acis.UI.States;

public sealed record DispatchNotificationEntryState(
    string Title,
    string SentAt,
    string StatusText,
    string Summary);
