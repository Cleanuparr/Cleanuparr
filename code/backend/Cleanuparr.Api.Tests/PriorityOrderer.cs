using Xunit.Abstractions;
using Xunit.Sdk;

namespace Cleanuparr.Api.Tests;

public sealed class PriorityOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        var sortedMethods = new SortedDictionary<int, List<TTestCase>>();

        foreach (var testCase in testCases)
        {
            var priority = testCase.TestMethod.Method
                .GetCustomAttributes(typeof(TestPriorityAttribute).AssemblyQualifiedName)
                .FirstOrDefault()
                ?.GetNamedArgument<int>("Priority") ?? 0;

            if (!sortedMethods.TryGetValue(priority, out var list))
            {
                list = [];
                sortedMethods[priority] = list;
            }

            list.Add(testCase);
        }

        foreach (var list in sortedMethods.Values)
        {
            foreach (var testCase in list)
            {
                yield return testCase;
            }
        }
    }
}
