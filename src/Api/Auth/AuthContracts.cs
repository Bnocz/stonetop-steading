namespace Api.Auth;

public sealed record LoginRequest(string? Username, string? Password);

public sealed record GmUserResponse(string Username);
