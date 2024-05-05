namespace TNAB.Parsers;

public class CustomList<T> : List<T>
{
    public override string ToString() => string.Format("[ {0} ]", string.Join(", ", this.Select(x => x?.ToString())));
}

public class CustomDictionary<TKey, TValue> : Dictionary<TKey, TValue> where TKey : notnull
{
    public override string ToString() => string.Format("{{ {0} }}", string.Join(", ", this.Select(x => $"{x.Key} = {x.Value}")));
}
