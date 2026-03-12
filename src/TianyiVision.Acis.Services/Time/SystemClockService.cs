namespace TianyiVision.Acis.Services.Time;

public sealed class SystemClockService : IClockService
{
    public DateTime GetCurrentTime() => DateTime.Now;
}
