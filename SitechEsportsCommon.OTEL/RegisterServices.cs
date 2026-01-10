using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace Sitech.Common;

public static class RegisterServices
{
    public const string CorrelationRequestHeaderId = "X-Correlation-Id";
    public const string CorrelationTagId = "CorrelationId";

    public static IServiceCollection RegisterOtel(this IServiceCollection services)
    {

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("sitech-esports"))
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .AddSource("sitech-esports")
                .AddProcessor(new CollaborationIdProcessor())
                .SetSampler(new AlwaysOnSampler())
             )
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .AddAspNetCoreInstrumentation((options) =>
                {
                    options.RecordException = true;
                    options.EnrichWithHttpRequest = (activity, httpRequest) =>
                    {
                        if (activity.IsAllDataRequested)
                        {
                            if (httpRequest.HttpContext?.Request.Headers.TryGetValue(CorrelationRequestHeaderId,
                                    out var value) == true
                                && !string.IsNullOrEmpty(value))
                            {
                                Baggage.SetBaggage(CorrelationTagId, value);
                                return;
                            }

                            Baggage.SetBaggage(CorrelationTagId, activity.TraceId.ToString());
                        }
                    };
                }
                ));

        services.AddSingleton(new ActivitySource("sitech-esports"));

        return services;
    }
}

    public class CollaborationIdProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity activity)
    {
        var correlationId = Baggage.GetBaggage(RegisterServices.CorrelationTagId);
        if (!string.IsNullOrEmpty(correlationId))
        {
            activity?.SetTag(RegisterServices.CorrelationTagId, correlationId);
        }
    }
}