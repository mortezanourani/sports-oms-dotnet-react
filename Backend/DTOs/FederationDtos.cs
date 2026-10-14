namespace Backend.DTOs
{
    // One shape for all three levels — which fields matter depends on
    // Level; the controller enforces which combination is valid.
    public record FederationCreateDto(
        string Level,
        string? Name,              // required for National only
        string? Category,          // optional for National; inherited from parent for Provincial/City
        Guid? ParentFederationId,  // required for Provincial/City
        Guid? ProvinceId,          // required for Provincial
        Guid? CityId);             // required for City

    public record FederationUpdateDto(string Name); // National only

    public record FederationOutDto(
        Guid Id, string DisplayName, string? Category, string Level,
        Guid? ParentFederationId, string? ParentFederationName,
        Guid? ProvinceId, string? ProvinceName,
        Guid? CityId, string? CityName,
        bool HasChildren);

    public record FederationRepAssignDto(
        string Username,
        string? Password,
        string? FirstName,
        string? LastName,
        string Position,
        DateOnly StartDate,
        DateOnly? EndDate);
}
