namespace GpnuNetwork.Core.Utils;

public static class LinqExtension
{
    public static IEnumerable<T> ExpandTreeDeepFirst<T>(this T root, Func<T, IEnumerable<T>> searchFunc)
    {
        foreach (var node in searchFunc(root))
        {
            yield return node;
            foreach (var child in node.ExpandTreeDeepFirst(searchFunc))
            {
                yield return child;
            }
        }
    }
}