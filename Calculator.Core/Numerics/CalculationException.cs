namespace Calculator.Core;

public enum CalculationError
{
    InvalidInput,
    DivideByZero,
    Domain,
    TooLarge
}

public sealed class CalculationException(string message, CalculationError code = CalculationError.InvalidInput)
    : Exception(message)
{
    public CalculationError Code { get; } = code;
}
