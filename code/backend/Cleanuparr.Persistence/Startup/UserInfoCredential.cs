namespace Cleanuparr.Persistence.Startup;

/// <summary>
/// A username/password pair decoded out of a URL's userinfo component.
/// </summary>
public readonly record struct UserInfoCredential(string? Username, string? Password);
