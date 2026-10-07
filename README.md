# Blipit

Blipit error monitoring for .NET. Know your app broke before your users tell you.

## Install

```sh
dotnet add package Blipit
```

## Quick start

Call `Init` once at startup and keep the returned handle alive for the life of the process.

```csharp
using Blipit;

using var blipit = Blipit.Blipit.Init(new BlipitOptions
{
    Key = "<public key>",
    Project = "<project id>",
    Environment = "production",
    Release = "myapp@1.4.2",
});
```

Both values are on the project's Keys page at app.blipit.io. Unhandled exceptions are reported on their own.

## ASP.NET Core

For request context, performance and log capture, add the `Sentry.AspNetCore` package and pass Blipit's DSN in `Program.cs`:

```csharp
builder.WebHost.UseSentry(Blipit.Blipit.Dsn("<public key>", "<project id>"));
```

If you only need exceptions, `Blipit.Blipit.Init(...)` at the top of `Program.cs` is enough.

## Manual capture

```csharp
Blipit.Blipit.CaptureException(ex);
Blipit.Blipit.CaptureMessage("cache miss rate above 50%", SentryLevel.Warning);
Blipit.Blipit.SetUser(id: "42", email: "ana@example.com");
Blipit.Blipit.SetTag("region", "ap-southeast-1");
Blipit.Blipit.AddBreadcrumb("opened checkout", "ui");
Blipit.Blipit.CaptureSecurity("login_failed", "ana@example.com", ip: "10.0.0.1");
Blipit.Blipit.Flush();
```

Login attempts (`CaptureSecurity`) need the project's secret key (init with it on the server) and the Scale plan; with the public key ingest refuses them with 403 `security_needs_secret_key`.

## Performance

Set `TracesSampleRate = 0.2` in the options and requests show up on the Performance page.

Docs: https://docs.blipit.io/dotnet
