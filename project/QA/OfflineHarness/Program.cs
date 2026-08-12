using System.Reflection;
using Xunit;

var assembly = Assembly.GetExecutingAssembly();
var tests = assembly.GetTypes()
    .OrderBy(t => t.FullName, StringComparer.Ordinal)
    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
        .Where(m => m.GetCustomAttribute<FactAttribute>() is not null)
        .OrderBy(m => m.Name, StringComparer.Ordinal)
        .Select(m => (Type: t, Method: m, Fact: m.GetCustomAttribute<FactAttribute>()!)))
    .ToArray();

var passed = 0;
var failed = 0;
var skipped = 0;
var failures = new List<string>();

foreach (var test in tests)
{
    var name = $"{test.Type.FullName}.{test.Method.Name}";
    if (!string.IsNullOrWhiteSpace(test.Fact.Skip))
    {
        skipped++;
        Console.WriteLine($"SKIP {name}: {test.Fact.Skip}");
        continue;
    }

    object? instance = null;
    try
    {
        if (!test.Method.IsStatic)
            instance = Activator.CreateInstance(test.Type) ?? throw new InvalidOperationException($"Could not create {test.Type.FullName}");
        var result = test.Method.Invoke(instance, null);
        if (result is Task task) task.GetAwaiter().GetResult();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (TargetInvocationException tie) when (tie.InnerException is not null)
    {
        failed++;
        var ex = tie.InnerException;
        failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
    }
    catch (Exception ex)
    {
        failed++;
        failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        if (instance is IDisposable disposable) disposable.Dispose();
    }
}

Console.WriteLine($"SUMMARY total={tests.Length} passed={passed} failed={failed} skipped={skipped}");
if (failures.Count > 0)
{
    Console.WriteLine("FAILURES:");
    foreach (var failure in failures) Console.WriteLine(failure);
}
return failed == 0 ? 0 : 1;
