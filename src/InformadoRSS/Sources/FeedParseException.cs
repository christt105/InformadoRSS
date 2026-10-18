namespace InformadoRSS.Sources;

/// <summary>The document is not well-formed XML or not an RSS or Atom feed.</summary>
public sealed class FeedParseException : Exception
{
    public FeedParseException()
    {
    }

    public FeedParseException(string message)
        : base(message)
    {
    }

    public FeedParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
