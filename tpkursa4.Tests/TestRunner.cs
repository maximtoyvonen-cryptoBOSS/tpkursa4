using System;
using System.Collections.Generic;
using System.Reflection;

namespace tpkursa4.Tests
{
    /// <summary>
    /// Минимальный тест-раннер без внешних зависимостей (работает offline).
    /// Тесты — методы без параметров, помеченные [Test]. Exit code: 0 — всё зелёное.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    public static class Assert
    {
        public static void True(bool condition, string message = "expected true")
        {
            if (!condition) throw new Exception("Assert.True failed: " + message);
        }

        public static void Equal<T>(T expected, T actual, string message = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"Assert.Equal failed: expected {expected}, actual {actual}. {message}");
        }

        public static void InRange(double value, double min, double max, string message = null)
        {
            if (value < min || value > max)
                throw new Exception($"Assert.InRange failed: {value} not in [{min},{max}]. {message}");
        }
    }

    public static class TestRunner
    {
        public static int Run()
        {
            int passed = 0, failed = 0;
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.GetCustomAttribute<TestAttribute>() == null) continue;
                    string name = type.Name + "." + m.Name;
                    try
                    {
                        m.Invoke(null, null);
                        Console.WriteLine($"PASS {name}");
                        passed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Console.WriteLine($"FAIL {name}: {(ex.InnerException ?? ex).Message}");
                    }
                }
            }
            Console.WriteLine($"--- {passed} passed, {failed} failed ---");
            return failed == 0 ? 0 : 1;
        }
    }
}
