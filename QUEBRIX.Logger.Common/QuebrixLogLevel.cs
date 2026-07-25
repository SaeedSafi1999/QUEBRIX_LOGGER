namespace QUEBRIX.Logger.Common;

/// <summary>
/// Defines QUEBRIX log levels aligned with Serilog levels.
/// </summary>
public enum QuebrixLogLevel
{
    /// <summary>
    /// Verbose/trace level logging.
    /// </summary>
    Verbose = 0,

    /// <summary>
    /// Debug level logging.
    /// </summary>
    Debug = 1,

    /// <summary>
    /// Informational messages.
    /// </summary>
    Information = 2,

    /// <summary>
    /// Warning messages.
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Error messages.
    /// </summary>
    Error = 4,

    /// <summary>
    /// Fatal/critical messages.
    /// </summary>
    Fatal = 5
}