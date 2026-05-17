namespace SmartScreen.Infrastructure.Ai;

internal readonly record struct PreparedAiImage(
    byte[] Bytes,
    string MimeType,
    int Width,
    int Height,
    bool WasOptimized,
    int OriginalByteCount);
