using System.Collections;
using System.Text;
using Analytics.Application.Common;
using Analytics.Infrastructure.Export;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Analytics.Api.Formatting;

/// <summary>
/// Serves any list endpoint as CSV when the client asks for it with <c>?format=csv</c>
/// or <c>Accept: text/csv</c>. A paged result exports its current page.
/// </summary>
internal sealed class CsvOutputFormatter : TextOutputFormatter
{
    public CsvOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/csv"));
        // UTF-8 with BOM so Excel opens Thai text correctly.
        SupportedEncodings.Add(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    protected override bool CanWriteType(Type? type) =>
        type is not null && (typeof(IPagedResult).IsAssignableFrom(type) || RowTypeOf(type) is not null);

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding encoding)
    {
        var (rows, rowType) = context.Object switch
        {
            IPagedResult page => (page.Rows, page.RowType),
            IEnumerable list => (list, RowTypeOf(context.ObjectType!)!),
            _ => throw new InvalidOperationException("CSV export needs a list."),
        };

        var http = context.HttpContext;
        var name = string.Join('-', http.Request.Path.Value!.Trim('/').Split('/').Skip(1));
        http.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{name}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv\"";

        await using var writer = context.WriterFactory(http.Response.Body, encoding);
        await CsvWriter.WriteAsync(writer, rows, rowType);
        await writer.FlushAsync();
    }

    private static Type? RowTypeOf(Type type) =>
        type == typeof(string)
            ? null
            : type.GetInterfaces().Append(type)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];
}
