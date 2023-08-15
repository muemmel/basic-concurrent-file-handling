namespace ConsoleApp1;

public class SymbolInfo
{
    public byte OriginalSymbol { get; set; }
    public byte[] TransformedSymbol { get; set; } = Array.Empty<byte>();
    public long NumOccurrences { get; set; }
}