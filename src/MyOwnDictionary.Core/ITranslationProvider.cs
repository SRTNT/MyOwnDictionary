namespace MyOwnDictionary.Core;

public interface ITranslationProvider
{
    Task<string> TranslateAsync(string text, Language source, Language target, CancellationToken cancellationToken);
}

public sealed class TranslationUnavailableException : Exception
{
    public TranslationUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
