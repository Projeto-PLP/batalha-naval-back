namespace BatalhaNaval.Domain.Exceptions;

public class TurnTimeoutException : Exception
{
    public TurnTimeoutException(string message) : base(message) { }
}
