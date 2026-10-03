namespace Cleanuparr.Shared.Attributes;

/// <summary>
/// Marks a property as containing sensitive data that should be masked in API responses
/// and preserved when the placeholder value is sent back in updates.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SensitiveDataAttribute : Attribute
{
    public SensitiveDataType Type { get; }

    public SensitiveDataAttribute(SensitiveDataType type = SensitiveDataType.Full)
    {
        Type = type;
    }
}
