namespace OpenSpidey.Assets.Archives;

public sealed record PkrEntry(string Path, uint Checksum, int Compression,
    long Offset, int DecodedSize, int StoredSize);
