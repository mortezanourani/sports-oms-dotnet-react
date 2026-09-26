namespace Backend.DTOs
{
    // What the Admin submits when creating a new account.
    // ScopeProvinceId/ScopeCityId are optional here because they only apply
    // depending on ScopeLevel — validated in the controller, not the DTO itself,
    // since that validation needs a database lookup (confirming the city/province
    // actually exist), which a DTO can't do on its own.
    public record UserCreateDto(
    string Username,
    string Password,
    string FirstName,
    string LastName,
    string Role, // matches UserRole enum names, e.g. "Admin", "M88Officer"
    string ScopeLevel, // matches ScopeLevel enum names: "Mastery", "Province", "City"
    Guid? ScopeProvinceId,
    Guid? ScopeCityId
    );

    // What the Admin submits when editing an existing account.
    // Password is intentionally excluded — password changes get their own
    // dedicated endpoint later, since that's a more sensitive action that
    // usually needs different handling (e.g. the user changing their own).
    public record UserUpdateDto(
    string FirstName,
    string LastName,
    string Role,
    string ScopeLevel,
    Guid? ScopeProvinceId,
    Guid? ScopeCityId
    );

    // What gets returned to the frontend — deliberately excludes any password
    // or security-related fields, only what's needed to display/manage a user.
    public record UserOutDto(
    Guid Id,
    string Username,
    string FirstName,
    string LastName,
    string Role,
    string ScopeLevel,
    Guid? ScopeProvinceId,
    string? ScopeProvinceName,
    Guid? ScopeCityId,
    string? ScopeCityName
    );

    // Submitted when an Admin assigns someone as a federation's
    // president/miss/vice. Deliberately separate from user creation, since
    // a user's role ("FederationRep") and their specific position/federation
    // are two different concerns tracked in two different tables — this keeps
    // FederationPosition's full history intact even if a user's role or scope
    // changes later for unrelated reasons.
    public record FederationPositionAssignDto(
    Guid FederationId,
    string Position, // "President", "Miss", or "Vice"
    DateOnly StartDate
    );

    public record FederationPositionOutDto(
    Guid Id,
    string Position,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid UserId,
    string UserFirstName,
    string UserLastName,
    Guid FederationId,
    string FederationName
    );
}