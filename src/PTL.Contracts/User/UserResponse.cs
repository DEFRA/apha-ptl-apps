namespace PTL.Contracts.User;

public sealed record UserResponse(Guid UserId, string Username, string FriendlyName, string FirstName, string LastName, string Email, string Department);
