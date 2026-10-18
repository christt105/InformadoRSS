namespace InformadoRSS.Storage;

public sealed record Feed(long Id, FeedSettings Settings, FeedHealth Health, FeedValidators Validators);
