using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ConcurrentBooking.Application.Repositories;
using ConcurrentBooking.Domain.Entities;

namespace Api.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyRepository idemRepo)
    {
        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
        {
            // continue without idempotency for endpoints that don't provide a key
            await _next(context);
            return;
        }

        var route = context.Request.Path.ToString();
        var existing = await idemRepo.GetByKeyAsync(key, route);
        if (existing != null && !string.IsNullOrEmpty(existing.ResponseBody))
        {
            _logger.LogInformation("Idempotency hit for key {Key} route {Route}", key, route);
            context.Response.StatusCode = existing.StatusCode ?? 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(existing.ResponseBody);
            return;
        }

        // Capture response
        var originalBody = context.Response.Body;
        await using var memStream = new MemoryStream();
        context.Response.Body = memStream;

        await _next(context);

        memStream.Seek(0, SeekOrigin.Begin);
        var respBody = await new StreamReader(memStream).ReadToEndAsync();
        memStream.Seek(0, SeekOrigin.Begin);

        // copy back to original stream
        await memStream.CopyToAsync(originalBody);
        context.Response.Body = originalBody;

        // persist idempotency record
        var record = new IdempotencyRecord(key, route)
        {
            ResponseBody = respBody,
            StatusCode = context.Response.StatusCode
        };

        try
        {
            await idemRepo.AddAsync(record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist idempotency record for key {Key}", key);
        }
    }
}
