namespace PuertoBB.Core.Exceptions;

public class ReciboException : Exception
{
    public ReciboException(string message) : base(message) { }
    public ReciboException(string message, Exception inner) : base(message, inner) { }
}
