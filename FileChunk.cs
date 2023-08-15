namespace ConsoleApp1;

public readonly record struct FileChunk(int Index, long Offset, long Length, Memory<byte> Data);