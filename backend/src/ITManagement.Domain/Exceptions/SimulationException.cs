namespace ITManagement.Domain.Exceptions;

public sealed class SimulationException(int statusCode, string code, string message) : DomainException(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
