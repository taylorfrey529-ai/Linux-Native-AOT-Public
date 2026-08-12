using System.Collections;

namespace Xunit;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class FactAttribute : Attribute
{
    public string? Skip { get; set; }
}

public sealed class XunitException : Exception
{
    public XunitException(string message) : base(message) { }
}

public static class Assert
{
    public static void True(bool condition, string? userMessage = null)
    {
        if (!condition) throw new XunitException(userMessage ?? "Assert.True() Failure");
    }

    public static void False(bool condition, string? userMessage = null)
    {
        if (condition) throw new XunitException(userMessage ?? "Assert.False() Failure");
    }

    public static void Null(object? value)
    {
        if (value is not null) throw new XunitException($"Assert.Null() Failure: {Format(value)}");
    }

    public static void NotNull(object? value)
    {
        if (value is null) throw new XunitException("Assert.NotNull() Failure");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!AreEqual(expected, actual))
            throw new XunitException($"Assert.Equal() Failure\nExpected: {Format(expected)}\nActual:   {Format(actual)}");
    }

    public static void NotEqual<T>(T notExpected, T actual)
    {
        if (AreEqual(notExpected, actual))
            throw new XunitException($"Assert.NotEqual() Failure\nValue: {Format(actual)}");
    }

    public static void Empty(IEnumerable collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        var e = collection.GetEnumerator();
        try
        {
            if (e.MoveNext()) throw new XunitException($"Assert.Empty() Failure: found {Format(e.Current)}");
        }
        finally
        {
            (e as IDisposable)?.Dispose();
        }
    }

    public static void NotEmpty(IEnumerable collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        var e = collection.GetEnumerator();
        try
        {
            if (!e.MoveNext()) throw new XunitException("Assert.NotEmpty() Failure");
        }
        finally
        {
            (e as IDisposable)?.Dispose();
        }
    }

    public static T Single<T>(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        using var e = collection.GetEnumerator();
        if (!e.MoveNext()) throw new XunitException("Assert.Single() Failure: collection empty");
        var value = e.Current;
        if (e.MoveNext()) throw new XunitException("Assert.Single() Failure: collection has more than one element");
        return value;
    }

    public static T Single<T>(IEnumerable<T> collection, Predicate<T> predicate)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(predicate);
        var matches = collection.Where(x => predicate(x)).ToArray();
        return Single(matches);
    }

    public static void Contains(string expectedSubstring, string actualString)
    {
        ArgumentNullException.ThrowIfNull(expectedSubstring);
        ArgumentNullException.ThrowIfNull(actualString);
        if (!actualString.Contains(expectedSubstring, StringComparison.Ordinal))
            throw new XunitException($"Assert.Contains() Failure: substring '{expectedSubstring}' not found");
    }

    public static void Contains<T>(T expected, IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        if (!collection.Any(x => AreEqual(expected, x)))
            throw new XunitException($"Assert.Contains() Failure: {Format(expected)} not found");
    }

    public static void Contains<T>(IEnumerable<T> collection, Predicate<T> predicate)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(predicate);
        if (!collection.Any(x => predicate(x)))
            throw new XunitException("Assert.Contains() Failure: predicate matched no elements");
    }

    public static void DoesNotContain(string expectedSubstring, string actualString)
    {
        ArgumentNullException.ThrowIfNull(expectedSubstring);
        ArgumentNullException.ThrowIfNull(actualString);
        if (actualString.Contains(expectedSubstring, StringComparison.Ordinal))
            throw new XunitException($"Assert.DoesNotContain() Failure: substring '{expectedSubstring}' found");
    }

    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        if (collection.Any(x => AreEqual(expected, x)))
            throw new XunitException($"Assert.DoesNotContain() Failure: {Format(expected)} found");
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> predicate)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(predicate);
        if (collection.Any(x => predicate(x)))
            throw new XunitException("Assert.DoesNotContain() Failure: predicate matched an element");
    }

    public static void All<T>(IEnumerable<T> collection, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(action);
        var index = 0;
        foreach (var item in collection)
        {
            try { action(item); }
            catch (Exception ex) { throw new XunitException($"Assert.All() Failure at index {index}: {ex.Message}"); }
            index++;
        }
    }

    public static void InRange<T>(T actual, T low, T high) where T : IComparable<T>
    {
        if (actual.CompareTo(low) < 0 || actual.CompareTo(high) > 0)
            throw new XunitException($"Assert.InRange() Failure: {actual} not in [{low}, {high}]");
    }

    public static TException Throws<TException>(Action action) where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (ex is TException expected) return expected;
            throw new XunitException($"Assert.Throws() Failure: expected {typeof(TException).FullName}, got {ex.GetType().FullName}: {ex.Message}");
        }
        throw new XunitException($"Assert.Throws() Failure: no exception thrown; expected {typeof(TException).FullName}");
    }

    private static bool AreEqual(object? expected, object? actual)
    {
        if (ReferenceEquals(expected, actual)) return true;
        if (expected is null || actual is null) return false;
        if (expected is string || actual is string) return expected.Equals(actual);
        if (expected is IEnumerable ee && actual is IEnumerable aa)
        {
            var left = ee.Cast<object?>().ToArray();
            var right = aa.Cast<object?>().ToArray();
            if (left.Length != right.Length) return false;
            for (var i = 0; i < left.Length; i++)
                if (!AreEqual(left[i], right[i])) return false;
            return true;
        }
        return expected.Equals(actual);
    }

    private static string Format(object? value)
    {
        if (value is null) return "<null>";
        if (value is string s) return '"' + s + '"';
        if (value is IEnumerable e) return "[" + string.Join(", ", e.Cast<object?>().Select(Format)) + "]";
        return value.ToString() ?? value.GetType().Name;
    }
}
