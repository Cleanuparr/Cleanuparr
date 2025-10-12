namespace Cleanuparr.Infrastructure.Models;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Details { get; set; } = new();
    
    public static ValidationResult Success() => new() { IsValid = true };
    
    public static ValidationResult Failure(string errorMessage, List<string>? details = null) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage,
        Details = details ?? new List<string>()
    };
}
