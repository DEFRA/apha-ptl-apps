namespace PTL.Contracts.User;

// Posted to POST /api/users - matches legacy Button_CreateUser_Click (the selected AD candidate's
// fields plus the admin-entered Department).
public sealed record CreateUserRequest(string Username, string Email, string FriendlyName, string FirstName, string LastName, string Department);
