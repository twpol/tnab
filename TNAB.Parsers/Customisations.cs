namespace TNAB.Parsers;

public class CustomList<T> : List<T>
{
    public CustomList() : base() { }
    public CustomList(IEnumerable<T> collection) : base(collection) { }
    public override string ToString() => string.Format("[ {0} ]", string.Join(", ", this.Select(x => x?.ToString())));
}

public class CustomDictionary<TKey, TValue> : Dictionary<TKey, TValue> where TKey : notnull
{
    public CustomDictionary() : base() { }
    public CustomDictionary(IDictionary<TKey, TValue> dictionary) : base(dictionary) { }
    public override string ToString() => string.Format("{{ {0} }}", string.Join(", ", this.Select(x => $"{x.Key} = {x.Value}")));
}
