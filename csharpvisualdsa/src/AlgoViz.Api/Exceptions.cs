namespace AlgoViz.Api;

/// <summary>Thrown when a script exceeds an operation, iteration or size limit.</summary>
public sealed class VizLimitException(string message) : Exception(message);

/// <summary>Thrown inside a script once its time limit has passed.</summary>
public sealed class VizTimeoutException() : Exception("The script ran out of time.");
