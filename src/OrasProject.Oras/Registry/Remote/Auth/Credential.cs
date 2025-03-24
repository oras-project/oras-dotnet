namespace OrasProject.Oras.Registry.Remote.Auth;

public record Credential(string? Username, string? Password, string? RefreshToken, string? AccessToken);
