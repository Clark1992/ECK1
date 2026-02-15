using System.Diagnostics;
using ECK1.CommonUtils.OpenTelemetry;

namespace ECK1.Integration.Cache.LongTerm.Services;

public interface INatsTelemetry
{
    Activity Start(string activityName, string operation, string bucket, string key = null);
    void SetError(Activity activity, Exception exception);
    void SetMiss(Activity activity);
    void SetCount(Activity activity, int count);
}

public class NatsTelemetry : INatsTelemetry
{
    public static readonly ActivitySource NatsActivitySource = new(DependencyOpenTelemetryExtensions.NatsActivitySourceName);

    public Activity Start(string activityName, string operation, string bucket, string key = null)
    {
        var activity = NatsActivitySource
            .StartActivity(activityName, ActivityKind.Client);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag("db.system", "nats");
        activity.SetTag("db.operation", operation);

        if (!string.IsNullOrWhiteSpace(bucket))
        {
            activity.SetTag("nats.bucket", bucket);
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            activity.SetTag("nats.key", key);
        }

        return activity;
    }

    public void SetError(Activity activity, Exception exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    public void SetMiss(Activity activity)
    {
        activity?.SetTag("nats.miss", true);
    }

    public void SetCount(Activity activity, int count)
    {
        activity?.SetTag("nats.count", count);
    }
}