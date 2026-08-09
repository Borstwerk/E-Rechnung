using EInvoiceSender.Core.Services;

namespace EInvoiceSender.App.Services;

/// <summary>Die Systemzeit als Port, damit Tests eine feste Zeit einsetzen koennen.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.Now;
}
