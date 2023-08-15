using System.Collections.Concurrent;

namespace ConsoleApp1;

public class TranslationCollection : Dictionary<byte, byte[]>
{
    private readonly ConcurrentDictionary<byte, SymbolInfo> translations =
        new();

    public void ProcessByte(byte value)
    {
        translations.AddOrUpdate(
            value,
            GenerateSymbolInfo,
            UpdateSymbolInfo);
    }

    private SymbolInfo GenerateSymbolInfo(byte value)
    {
        return new SymbolInfo
        {
            OriginalSymbol = value,
            TransformedSymbol = new []{value},
            NumOccurrences = 1
        };
    }

    private SymbolInfo UpdateSymbolInfo(byte value, SymbolInfo symbolInfo)
    {
        symbolInfo.NumOccurrences++;
        return symbolInfo;
    }

    public void Generate()
    {
        foreach (var (b, info) in translations)
            this[b] = info.TransformedSymbol;
    }
}