using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BlipitTests
{
    public class BlipitTests
    {
        [Fact]
        public void DsnIsBuiltFromKeyAndProject()
        {
            Assert.Equal("https://blipit_pk_abc@in.blipit.io/7", Blipit.Blipit.Dsn("blipit_pk_abc", "7"));
            Assert.Equal("http://k@127.0.0.1:8080/9", Blipit.Blipit.Dsn("k", "9", "http://127.0.0.1:8080/"));
            Assert.Throws<ArgumentException>(() => Blipit.Blipit.Init(new Blipit.BlipitOptions { Project = "7" }));
        }

        [Fact]
        public void SecurityContextDropsEmptyFields()
        {
            var context = Blipit.Blipit.SecurityContext("login_failed", "ana@example.com", ip: "10.0.0.1", target: "/login");
            Assert.Equal(new[] { "kind", "actor", "ip", "target" }, context.Keys.ToArray());
            Assert.Equal("10.0.0.1", context["ip"]);
        }

        [Fact]
        public async Task EventsReachBlipit()
        {
            var port = FreePort();
            var received = new ConcurrentQueue<(string Path, string Body)>();
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            var serving = Task.Run(() => Serve(listener, received));

            using (Blipit.Blipit.Init(new Blipit.BlipitOptions { Key = "blipit_pk_abc", Project = "7", Environment = "test", Endpoint = $"http://127.0.0.1:{port}" }))
            {
                Blipit.Blipit.CaptureMessage("hello from dotnet");
                Blipit.Blipit.Flush(TimeSpan.FromSeconds(10));
            }

            listener.Stop();
            await serving;

            var envelope = received.FirstOrDefault(r => r.Body.Contains("hello from dotnet"));
            Assert.NotNull(envelope.Body);
            Assert.Equal("/api/7/envelope/", envelope.Path);
            Assert.Contains("\"environment\":\"test\"", envelope.Body);
        }

        private static async Task Serve(HttpListener listener, ConcurrentQueue<(string, string)> received)
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); }
                catch { return; }
                Stream input = ctx.Request.InputStream;
                if (ctx.Request.Headers["Content-Encoding"] == "gzip") input = new GZipStream(input, CompressionMode.Decompress);
                using var ms = new MemoryStream();
                await input.CopyToAsync(ms);
                received.Enqueue((ctx.Request.Url!.AbsolutePath, Encoding.UTF8.GetString(ms.ToArray())));
                var ok = Encoding.UTF8.GetBytes("{}");
                ctx.Response.StatusCode = 200;
                await ctx.Response.OutputStream.WriteAsync(ok, 0, ok.Length);
                ctx.Response.Close();
            }
        }

        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
