using Bookshelf.Services.Storage;

namespace Bookshelf.Features.Dashboard;

/// <summary>Dashboard settings, remembered on this device.</summary>
public sealed class DashboardPreferences(ILocalStore store)
{
    internal const string PeriodKey = "stats.period";

    public event Action? Changed;

    public async Task<StatsPeriod> GetPeriodAsync() =>
        Enum.TryParse<StatsPeriod>(await store.GetSettingAsync(PeriodKey), out var period) ? period : StatsPeriod.CalendarYear;

    public async Task SetPeriodAsync(StatsPeriod period)
    {
        await store.SetSettingAsync(PeriodKey, period.ToString());
        Changed?.Invoke();
    }
}
