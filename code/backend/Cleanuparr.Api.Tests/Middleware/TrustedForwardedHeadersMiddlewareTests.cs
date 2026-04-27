using System.Net;
using Cleanuparr.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace Cleanuparr.Api.Tests.Middleware;

public class TrustedForwardedHeadersMiddlewareTests
{
    private static HttpContext NewContext(IPAddress peer, Action<HttpContext>? configure = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = peer;
        ctx.Request.Scheme = "http";
        ctx.Request.Host = new HostString("backend.local");
        configure?.Invoke(ctx);
        return ctx;
    }

    [Fact]
    public void Untrusted_direct_peer_leaves_everything_alone()
    {
        var ctx = NewContext(IPAddress.Parse("203.0.113.1"), c =>
        {
            c.Request.Headers["X-Forwarded-For"] = "10.0.0.5";
            c.Request.Headers["X-Forwarded-Proto"] = "https";
            c.Request.Headers["X-Forwarded-Host"] = "spoofed.example.com";
        });

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string>());

        ctx.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("203.0.113.1"));
        ctx.Request.Scheme.ShouldBe("http");
        ctx.Request.Host.Value.ShouldBe("backend.local");
    }

    [Fact]
    public void Local_peer_no_xff_is_a_no_op()
    {
        var ctx = NewContext(IPAddress.Loopback);

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string>());

        ctx.Connection.RemoteIpAddress.ShouldBe(IPAddress.Loopback);
    }

    [Fact]
    public void Spoofed_local_xff_with_appended_attacker_promotes_attacker_ip_only()
    {
        var ctx = NewContext(IPAddress.Loopback, c =>
            c.Request.Headers["X-Forwarded-For"] = "10.0.0.5, 99.99.99.99");

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string>());

        ctx.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("99.99.99.99"));
    }

    [Fact]
    public void Single_xff_entry_from_overwrite_mode_proxy_becomes_client_ip()
    {
        var ctx = NewContext(IPAddress.Loopback, c => c.Request.Headers["X-Forwarded-For"] = "203.0.113.45");

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string>());

        ctx.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("203.0.113.45"));
    }

    [Fact]
    public void Legitimate_lan_client_through_local_proxy_resolves_to_lan_ip()
    {
        var ctx = NewContext(IPAddress.Loopback, c => c.Request.Headers["X-Forwarded-For"] = "192.168.1.50");

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string>());

        ctx.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("192.168.1.50"));
    }

    [Fact]
    public void Custom_trusted_network_pops_through_to_real_client()
    {
        var ctx = NewContext(IPAddress.Loopback, c => c.Request.Headers["X-Forwarded-For"] = "100.64.1.5, 100.64.0.7");

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string> { "100.64.0.0/10" });

        ctx.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("100.64.1.5"));
    }

    [Fact]
    public void Forwarded_proto_and_host_applied_when_chain_consumed()
    {
        var ctx = NewContext(IPAddress.Loopback, c =>
        {
            c.Request.Headers["X-Forwarded-For"] = "203.0.113.45";
            c.Request.Headers["X-Forwarded-Proto"] = "https";
            c.Request.Headers["X-Forwarded-Host"] = "cleanuparr.example.com";
        });

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string>());

        ctx.Request.Scheme.ShouldBe("https");
        ctx.Request.Host.Value.ShouldBe("cleanuparr.example.com");
    }

    [Fact]
    public void Forwarded_proto_not_applied_when_peer_untrusted()
    {
        var ctx = NewContext(IPAddress.Parse("203.0.113.1"), c => c.Request.Headers["X-Forwarded-Proto"] = "https");

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string>());

        ctx.Request.Scheme.ShouldBe("http");
    }

    [Fact]
    public void X_real_ip_is_ignored()
    {
        var ctx = NewContext(IPAddress.Loopback, c => c.Request.Headers["X-Real-IP"] = "10.0.0.5");

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string>());

        ctx.Connection.RemoteIpAddress.ShouldBe(IPAddress.Loopback);
    }

    [Fact]
    public void Malformed_xff_entry_fails_closed()
    {
        var ctx = NewContext(IPAddress.Loopback, c =>
        {
            c.Request.Headers["X-Forwarded-For"] = "10.0.0.5, not-an-ip";
            c.Request.Headers["X-Forwarded-Proto"] = "https";
        });

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string>());

        ctx.Connection.RemoteIpAddress.ShouldBe(IPAddress.Loopback);
        ctx.Request.Scheme.ShouldBe("http");
    }

    [Fact]
    public void Empty_entries_in_xff_are_skipped()
    {
        // nginx with `proxy_set_header X-Forwarded-For "$http_x_forwarded_for, 1.2.3.4"`
        // produces a leading empty entry when the input header was absent.
        var ctx = NewContext(IPAddress.Loopback, c => c.Request.Headers["X-Forwarded-For"] = ", 99.99.99.99");

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string>());

        ctx.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("99.99.99.99"));
    }

    [Fact]
    public void Ipv4_mapped_ipv6_loopback_is_treated_as_trusted()
    {
        // Kestrel may surface "::ffff:127.0.0.1" as the peer.
        var mapped = IPAddress.Parse("::ffff:127.0.0.1");
        var ctx = NewContext(mapped, c => c.Request.Headers["X-Forwarded-For"] = "203.0.113.45");

        TrustedForwardedHeadersMiddleware.ApplyForwardedHeaders(ctx, new List<string>());

        ctx.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("203.0.113.45"));
    }
}
