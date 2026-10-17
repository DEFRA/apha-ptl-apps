namespace PTL.Core.User;

// The details needed to create a PT-LIMS account, grouped so CreateUserAsync stays within
// SonarCloud's seven-parameter limit.
public sealed record NewUserDetails(
    string Username,
    string Email,
    string FriendlyName,
    string FirstName,
    string LastName,
    string Department);
