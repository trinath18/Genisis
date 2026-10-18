namespace Genisis.Api.Common.Errors;

/// <summary>
/// A business rule stopped the operation (for example "There is at least an outstanding case under this member!"). The message is
/// written for the user and is returned as a 400 Problem Details response by <see cref="ProblemDetailsExceptionHandler"/>.
/// Use it for rules that can only be checked inside a transaction; check request shape in the feature's validator instead, and
/// return <c>null</c> for a record that does not exist.
/// </summary>
public class BusinessRuleException(string message) : Exception(message);
