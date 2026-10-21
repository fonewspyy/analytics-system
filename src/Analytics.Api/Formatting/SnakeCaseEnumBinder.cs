using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Analytics.Api.Formatting;

/// <summary>
/// Query-string enums use the same snake_case labels as JSON bodies and the database
/// (?status=in_progress). The default binder only accepts "InProgress" or integers.
/// </summary>
internal sealed class SnakeCaseEnumBinder(Type enumType) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var raw = context.ValueProvider.GetValue(context.ModelName).FirstValue;
        if (string.IsNullOrEmpty(raw))
            return Task.CompletedTask;

        var match = Enum.GetValues(enumType).Cast<Enum>()
            .FirstOrDefault(v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()) == raw);

        if (match is null)
        {
            var allowed = string.Join(", ", Enum.GetNames(enumType).Select(JsonNamingPolicy.SnakeCaseLower.ConvertName));
            context.ModelState.TryAddModelError(context.ModelName, $"'{raw}' is not one of: {allowed}.");
            context.Result = ModelBindingResult.Failed();
        }
        else
        {
            context.Result = ModelBindingResult.Success(match);
        }
        return Task.CompletedTask;
    }
}

internal sealed class SnakeCaseEnumBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return type.IsEnum && context.BindingInfo.BindingSource == BindingSource.Query
            ? new SnakeCaseEnumBinder(type)
            : null;
    }
}
