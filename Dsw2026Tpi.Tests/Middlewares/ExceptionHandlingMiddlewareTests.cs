using System.Text;
using System.Text.Json;
using Dsw2026Tpi.Api.Middlewares;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dsw2026Tpi.Tests.Middlewares;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task UnhandledExceptionReplacesUnsentBodyWithUniform500()
    {
        var context = CreateContext();
        var partialBody = Encoding.UTF8.GetBytes("contenido parcial");

        context.Response.ContentType = "text/plain";
        await context.Response.Body.WriteAsync(partialBody);

        var middleware = CreateMiddleware(_ =>
            Task.FromException(new InvalidOperationException(
                "detalle-interno-no-publico")));

        await middleware.InvokeAsync(context);

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);

        Assert.StartsWith(
            "application/json",
            context.Response.ContentType!);

        context.Response.Body.Position = 0;

        using var json = await JsonDocument.ParseAsync(
            context.Response.Body);

        Assert.Equal(
            "UNHANDLED_ERROR",
            json.RootElement.GetProperty("errorCode").GetString());

        Assert.False(string.IsNullOrWhiteSpace(
            json.RootElement.GetProperty("message").GetString()));

        Assert.DoesNotContain(
            "detalle-interno-no-publico",
            json.RootElement.GetRawText());
    }

    [Fact]
    public async Task ConflictPreservesItsBusinessCode()
    {
        var context = CreateContext();

        var middleware = CreateMiddleware(_ =>
            Task.FromException(new ConflictException(
                "Conflicto de prueba.",
                "TEST_CONFLICT")));

        await middleware.InvokeAsync(context);

        Assert.Equal(
            StatusCodes.Status409Conflict,
            context.Response.StatusCode);

        context.Response.Body.Position = 0;

        using var json = await JsonDocument.ParseAsync(
            context.Response.Body);

        Assert.Equal(
            "TEST_CONFLICT",
            json.RootElement.GetProperty("errorCode").GetString());

        Assert.Equal(
            "Conflicto de prueba.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task StartedResponseIsNotRewritten()
    {
        var context = new DefaultHttpContext();

        context.Features.Set<IHttpResponseFeature>(
            new StartedResponseFeature());

        context.Response.Body = new MemoryStream();
        context.Response.StatusCode = StatusCodes.Status202Accepted;

        await context.Response.Body.WriteAsync(
            Encoding.UTF8.GetBytes("respuesta iniciada"));

        var failure = new InvalidOperationException("fallo posterior");

        var middleware = CreateMiddleware(_ =>
            Task.FromException(failure));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));

        Assert.Same(failure, actual);

        Assert.Equal(
            StatusCodes.Status202Accepted,
            context.Response.StatusCode);

        Assert.Equal(
            "respuesta iniciada",
            Encoding.UTF8.GetString(
                ((MemoryStream)context.Response.Body).ToArray()));
    }

    [Fact]
    public async Task RequestCancellationIsNotConvertedTo500()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var context = CreateContext();
        context.RequestAborted = cancellation.Token;

        var failure = new OperationCanceledException(
            cancellation.Token);

        var middleware = CreateMiddleware(_ =>
            Task.FromException(failure));

        var actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => middleware.InvokeAsync(context));

        Assert.Same(failure, actual);
        Assert.Equal(0L, context.Response.Body.Length);
        Assert.Equal(
            StatusCodes.Status200OK,
            context.Response.StatusCode);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ExceptionHandlingMiddleware CreateMiddleware(
        RequestDelegate next)
    {
        return new ExceptionHandlingMiddleware(
            next,
            NullLogger<ExceptionHandlingMiddleware>.Instance);
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
