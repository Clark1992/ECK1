using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace ECK1.CommonUtils.AspNet;

public partial class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    public string TransformOutbound(object value)
    {
        if (value == null)
            return null;

        return SlugifyRegex().Replace(value.ToString(), "$1-$2").ToLower();
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex SlugifyRegex();
}
