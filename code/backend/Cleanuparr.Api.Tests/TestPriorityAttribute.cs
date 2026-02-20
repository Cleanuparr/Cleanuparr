namespace Cleanuparr.Api.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestPriorityAttribute : Attribute
{
    public int Priority { get; }

    public TestPriorityAttribute(int priority)
    {
        Priority = priority;
    }
}
