namespace InformadoRSS.Core;

public enum FetchErrorKind
{
    /// <summary>The server answered with a non-success status code (see <see cref="FetchError.StatusCode"/>).</summary>
    HttpStatus,

    /// <summary>The request or the download of the body took longer than the configured timeout.</summary>
    Timeout,

    /// <summary>Connection-level failure: DNS, TCP, TLS, connection reset.</summary>
    Network,

    /// <summary>The body was downloaded but is not a well-formed RSS or Atom document.</summary>
    Parse,

    /// <summary>The body exceeded the configured size limit.</summary>
    TooLarge,
}
