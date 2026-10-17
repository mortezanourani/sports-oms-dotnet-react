namespace Backend.DTOs
{
    public record ProfileOutDto(
        string Username,
        string FirstName,
        string LastName
    );

    public record ProfileUpdateDto(
        string FirstName,
        string LastName
    );

    public record PasswordChangeDto(
        string CurrentPassword,
        string NewPassword
    );
}
