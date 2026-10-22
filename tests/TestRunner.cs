using System.Reflection;
namespace Stokin.Testing;

[AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute(bool sql = false, bool benchmark = false, bool backup = false) : Attribute { public bool Sql { get; } = sql; public bool Benchmark { get; } = benchmark; public bool Backup { get; } = backup; }
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)] public sealed class CaseAttribute(params object[] values) : Attribute { public object[] Values => values; }
public static class Assert
{
    public static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, actual {actual}"); }
    public static void True(bool value, string message = "Expected true") { if (!value) throw new Exception(message); }
    public static void Null(object? value) => True(value is null, "Expected null");
    public static void DoesNotContain<T>(IEnumerable<T> items, Func<T, bool> match) => True(!items.Any(match), "Unexpected item");
    public static T Throws<T>(Action action) where T : Exception { try { action(); } catch (T e) { return e; } throw new Exception($"Expected {typeof(T).Name}"); }
    public static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T e) { return e; } throw new Exception($"Expected {typeof(T).Name}"); }
}
public static class Runner
{
    public static async Task<int> RunAsync(Assembly assembly, string[] args)
    {
        int pass = 0, fail = 0, skip = 0; string filter = args.FirstOrDefault() ?? "";
        foreach (var type in assembly.GetTypes()) foreach (var method in type.GetMethods().Where(m => m.IsDefined(typeof(TestAttribute))))
        {
            if (!(type.Name + "." + method.Name).Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            var test = method.GetCustomAttribute<TestAttribute>()!;
            if ((test.Sql && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOKIN_TEST_CONNECTION"))) || (test.Benchmark && Environment.GetEnvironmentVariable("STOKIN_RUN_BENCHMARK") != "1") || (test.Backup && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOKIN_SQL_BACKUP_DIR")))) { Console.WriteLine($"SKIP {type.Name}.{method.Name}: perlu STOKIN_TEST_CONNECTION; benchmark/backup juga memerlukan opt-in tersendiri."); skip++; continue; }
            var cases = method.GetCustomAttributes<CaseAttribute>().Select(c => c.Values).ToList(); if (cases.Count == 0) cases.Add([]);
            foreach (var values in cases) try
                {
                    var instance = Activator.CreateInstance(type); var result = method.Invoke(instance, values);
                    if (result is Task task) await task;
                    Console.WriteLine($"PASS {type.Name}.{method.Name}"); pass++;
                }
                catch (Exception e) { Console.WriteLine($"FAIL {type.Name}.{method.Name}: {(e is TargetInvocationException ? e.InnerException : e)}"); fail++; }
        }
        Console.WriteLine($"RESULT passed={pass} failed={fail} skipped={skip}"); return fail == 0 && (pass > 0 || skip > 0) ? 0 : 1;
    }
}
