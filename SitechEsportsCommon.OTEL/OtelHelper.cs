using System.Diagnostics;

namespace Sitech.Common;

public class OtelHelper
{
    public static readonly ActivitySource PricingActivitySource = new ActivitySource("Pricing");

    private readonly ActivitySource _activitySource;

    public OtelHelper(string activitySourceName)
    {
        _activitySource = new ActivitySource(activitySourceName);
    }

    public T StartActivity<T>(string activityName, Func<T> implementation)
    {
        using var activity = _activitySource.StartActivity(activityName);
        return implementation();
    }
}
