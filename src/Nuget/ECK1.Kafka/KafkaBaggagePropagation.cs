using System.Diagnostics;
using System.Text;
using Confluent.Kafka;

namespace ECK1.Kafka;

/// <summary>
/// Fills the gap in Confluent.Kafka.Extensions.Diagnostics which propagates
/// only traceparent/tracestate but NOT the W3C baggage header.
/// </summary>
internal static class KafkaBaggagePropagation
{
    private const string BaggageHeaderName = "baggage";

    /// <summary>
    /// Serializes <see cref="Activity.Current"/> baggage items into a W3C
    /// <c>baggage</c> Kafka header on the supplied message.
    /// </summary>
    internal static void InjectBaggage<TKey, TValue>(Message<TKey, TValue> message)
    {
        var activity = Activity.Current;
        if (activity is null)
            return;

        var baggage = activity.Baggage;
        var sb = new StringBuilder();

        foreach (var (key, value) in baggage)
        {
            if (string.IsNullOrEmpty(key))
                continue;

            if (sb.Length > 0)
                sb.Append(',');

            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value ?? string.Empty));
        }

        if (sb.Length == 0)
            return;

        message.Headers ??= new Headers();
        message.Headers.Add(BaggageHeaderName, Encoding.UTF8.GetBytes(sb.ToString()));
    }

    /// <summary>
    /// Reads the W3C <c>baggage</c> Kafka header and sets the items as
    /// baggage on <see cref="Activity.Current"/>.
    /// </summary>
    internal static void ExtractBaggage(Headers headers)
    {
        var activity = Activity.Current;
        if (activity is null || headers is null)
            return;

        var header = headers.FirstOrDefault(h => h.Key == BaggageHeaderName);
        if (header is null)
            return;

        var raw = Encoding.UTF8.GetString(header.GetValueBytes());

        foreach (var pair in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var eqIndex = pair.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = Uri.UnescapeDataString(pair[..eqIndex]);
            var value = Uri.UnescapeDataString(pair[(eqIndex + 1)..]);
            activity.SetBaggage(key, value);
        }
    }
}
