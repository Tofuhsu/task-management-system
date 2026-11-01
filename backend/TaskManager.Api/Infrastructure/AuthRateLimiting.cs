using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TaskManager.Api.Configuration;

namespace TaskManager.Api.Infrastructure;

public static class AuthRateLimiting
{
    // One sliding window per client IP. Behind a reverse proxy the IP comes from
    // X-Forwarded-For via ForwardedHeaders (ASPNETCORE_FORWARDEDHEADERS_ENABLED).
    // The limiter is in memory, so the budget is per application instance.
    public static RateLimitPartition<string> Partition(HttpContext context)
    {
        var options = context.RequestServices
            .GetRequiredService<IOptions<AuthRateLimitOptions>>()
            .Value;

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = TimeSpan.FromSeconds(options.WindowSeconds),
                SegmentsPerWindow = options.SegmentsPerWindow,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }

    public static async ValueTask WriteRejectionAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        // The sliding-window limiter does not attach RetryAfter metadata, so fall back
        // to one segment: the earliest moment any expired permits are returned.
        if (!context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var options = httpContext.RequestServices
                .GetRequiredService<IOptions<AuthRateLimitOptions>>()
                .Value;
            retryAfter = TimeSpan.FromSeconds(
                (double)options.WindowSeconds / Math.Max(1, options.SegmentsPerWindow));
        }

        httpContext.Response.Headers.RetryAfter =
            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(AuthRateLimiting))
            .LogWarning(
                "Rate limited {Method} {Path} from {RemoteIp}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.Connection.RemoteIpAddress);

        var problemDetailsService = httpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many attempts.",
                Detail = "Wait before trying to sign in or register again."
            }
        });
    }
}
