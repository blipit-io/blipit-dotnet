using System;
using System.Collections.Generic;
using Sentry;

namespace Blipit
{
    public sealed class BlipitOptions
    {
        public string Key { get; set; } = "";
        public string Project { get; set; } = "";
        public string? Environment { get; set; }
        public string? Release { get; set; }
        public double TracesSampleRate { get; set; }
        public string Endpoint { get; set; } = Blipit.DefaultEndpoint;
        public Action<SentryOptions>? Configure { get; set; }
    }

    public static class Blipit
    {
        public const string DefaultEndpoint = "https://in.blipit.io";
        public const string PublicKeyWarning = "[blipit] login attempts need the project's secret key (blipit_sk_...). This SDK was started with the public key, so CaptureSecurity sends nothing. Use the secret key on the server.";

        private static volatile bool publicKey;
        private static int publicKeyWarned;

        public static string Dsn(string key, string project, string endpoint = DefaultEndpoint)
        {
            var sep = endpoint.IndexOf("://", StringComparison.Ordinal);
            var host = (sep >= 0 ? endpoint.Substring(sep + 3) : endpoint).TrimEnd('/');
            var scheme = endpoint.StartsWith("http://", StringComparison.Ordinal) ? "http" : "https";
            return $"{scheme}://{key}@{host}/{project}";
        }

        public static IDisposable Init(BlipitOptions options)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.Key)) throw new ArgumentException("Blipit.Init needs the project's public key", nameof(options));
            if (string.IsNullOrEmpty(options.Project)) throw new ArgumentException("Blipit.Init needs the project id", nameof(options));
            return SentrySdk.Init(o => Apply(o, options));
        }

        public static void Apply(SentryOptions sentry, BlipitOptions options)
        {
            publicKey = options.Key.StartsWith("blipit_pk_", StringComparison.Ordinal);
            sentry.Dsn = Dsn(options.Key, options.Project, options.Endpoint);
            sentry.Environment = options.Environment;
            sentry.Release = options.Release;
            sentry.TracesSampleRate = options.TracesSampleRate;
            sentry.SendDefaultPii = false;
            options.Configure?.Invoke(sentry);
        }

        public static SentryId CaptureException(Exception exception) => SentrySdk.CaptureException(exception);

        public static SentryId CaptureMessage(string message, SentryLevel level = SentryLevel.Info) => SentrySdk.CaptureMessage(message, level);

        public static void SetUser(string? id, string? email = null, string? username = null)
        {
            SentrySdk.ConfigureScope(scope => scope.User = new SentryUser { Id = id, Email = email, Username = username });
        }

        public static void SetTag(string key, string value) => SentrySdk.ConfigureScope(scope => scope.SetTag(key, value));

        public static void AddBreadcrumb(string message, string? category = null, BreadcrumbLevel level = BreadcrumbLevel.Info)
        {
            SentrySdk.AddBreadcrumb(message, category, level: level);
        }

        public static Dictionary<string, string> SecurityContext(string kind, string actor, string? outcome = null, string? actorId = null, string? ip = null, string? userAgent = null, string? target = null)
        {
            var context = new Dictionary<string, string> { ["kind"] = kind, ["actor"] = actor };
            if (outcome != null) context["outcome"] = outcome;
            if (actorId != null) context["actor_id"] = actorId;
            if (ip != null) context["ip"] = ip;
            if (userAgent != null) context["user_agent"] = userAgent;
            if (target != null) context["target"] = target;
            return context;
        }

        public static SentryId CaptureSecurity(string kind, string actor, string? outcome = null, string? actorId = null, string? ip = null, string? userAgent = null, string? target = null)
        {
            if (publicKey)
            {
                if (System.Threading.Interlocked.Exchange(ref publicKeyWarned, 1) == 0) Console.Error.WriteLine(PublicKeyWarning);
                return SentryId.Empty;
            }
            var context = SecurityContext(kind, actor, outcome, actorId, ip, userAgent, target);
            var level = kind == "login_failed" || kind == "login_blocked" ? SentryLevel.Warning : SentryLevel.Info;
            return SentrySdk.CaptureMessage($"{kind} for {actor}", scope =>
            {
                scope.Contexts["security"] = context;
                scope.SetTag("security.kind", kind);
            }, level);
        }

        public static void Flush(TimeSpan? timeout = null) => SentrySdk.Flush(timeout ?? TimeSpan.FromSeconds(2));
    }
}
