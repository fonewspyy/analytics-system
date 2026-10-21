using System.Text.Json;
using System.Text.Json.Serialization;
using Analytics.Api.Errors;
using Analytics.Api.Formatting;
using Analytics.Api.Health;
using Analytics.Application;
using Analytics.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services
    .AddControllers(options =>
    {
        // ?format=csv or Accept: text/csv on any list endpoint.
        options.RespectBrowserAcceptHeader = true;
        options.OutputFormatters.Add(new CsvOutputFormatter());
        options.FormatterMappings.SetMediaTypeMappingForFormat("csv", "text/csv");
        options.FormatterMappings.SetMediaTypeMappingForFormat("json", "application/json");
        options.Filters.Add(new FormatFilterAttribute());
        options.ModelBinderProviders.Insert(0, new SnakeCaseEnumBinderProvider());
    })
    .AddJsonOptions(options =>
    {
        // Enum values on the wire match the database labels: "in_progress", "on_hold".
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
    });

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions.TryAdd("traceId", ctx.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks()
    // A down database should fail the probe quickly, not hang it until the caller times out.
    .AddCheck<DatabaseHealthCheck>("postgres", timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();   // bare 404/405 also come back as ProblemDetails

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
