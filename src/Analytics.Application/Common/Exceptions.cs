namespace Analytics.Application.Common;

/// <summary>Maps to 404.</summary>
public sealed class NotFoundException(string entity, long id)
    : Exception($"{entity} {id} was not found.");

/// <summary>Maps to 409: the request conflicts with data that already exists.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Maps to 400 with field-level errors, same shape as model-binding errors.</summary>
public sealed class RequestValidationException(string field, string message)
    : Exception(message)
{
    public IDictionary<string, string[]> Errors { get; } =
        new Dictionary<string, string[]> { [field] = [message] };
}
