namespace Analytics.Domain;

/// <summary>The request is well-formed but conflicts with the current state of the entity.</summary>
public sealed class DomainRuleException(string message) : Exception(message);
