namespace AnkiHistoryVisualization;

public class MethodCache<T, TR>
    where T : notnull
{
    private readonly Dictionary<T, TR> dic = [];

    public TR Get(T input, Func<T, TR> method)
    {
        if (!dic.TryGetValue(input, out var output))
        {
            output = method(input);
            dic.Add(input, output);
        }

        return output;
    }
}
