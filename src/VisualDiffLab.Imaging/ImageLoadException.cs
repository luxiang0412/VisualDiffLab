namespace VisualDiffLab.Imaging;

public enum ImageLoadErrorKind
{
    UnsupportedFormat,
    FileNotFound,
    AccessDenied,
    CorruptFile,
    TooLarge,
    EmptyImage,
    Unknown,
}

/// <summary>A load failure with a user-facing message and a diagnostic kind.</summary>
public sealed class ImageLoadException : Exception
{
    public ImageLoadException(ImageLoadErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public ImageLoadErrorKind Kind { get; }

    public string UserMessage => Message;
}
