namespace ConsoleApp1;

public static class Extensions
{
    public static TValue GetOrAddDefault<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue? defaultValue = default)
        where TValue : new()
    {
        if (!dictionary.TryGetValue(key, out var value))
            dictionary[key] = value = defaultValue ?? new TValue();

        return value;
    }

    public static TValue GetOrAddDefault<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TValue?>? defaultValue = null)
        where TValue : new()
    {
        defaultValue ??= static () => new TValue();
        if (!dictionary.TryGetValue(key, out var value))
            dictionary[key] = value = defaultValue.Invoke() ?? new TValue();

        return value;
    }
}