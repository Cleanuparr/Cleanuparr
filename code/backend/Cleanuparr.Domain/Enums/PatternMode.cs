namespace Cleanuparr.Domain.Enums;

public enum PatternMode
{
    /// <summary>
    /// Delete all except those that match the patterns
    /// </summary>
    Exclude,
    
    /// <summary>
    /// Delete only those that match the patterns
    /// </summary>
    Include
}