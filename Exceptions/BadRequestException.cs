namespace Cw_7_s30976.Exceptions;

public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}