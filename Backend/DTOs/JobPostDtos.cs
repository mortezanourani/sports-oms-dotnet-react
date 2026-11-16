namespace Backend.DTOs
{
    public record JobPostUpsertDto(
        string Name,
        string PlacementScope,
        bool IsActive
    );

    public record JobPostOutDto(
        Guid Id,
        string Name,
        string PlacementScope,
        bool IsActive
    );

    public record JobPostAssignmentCreateDto(
        Guid JobPostId,
        Guid UserId,
        Guid? ProvinceId,
        Guid? CityId,
        DateOnly StartedOn
    );

    public record JobPostAssignmentEndDto(DateOnly EndedOn);

    public record JobPostAssignmentOutDto(
        Guid Id,
        Guid JobPostId,
        string JobPostName,
        Guid UserId,
        string Username,
        string UserFirstName,
        string UserLastName,
        Guid ProvinceId,
        string ProvinceName,
        Guid? CityId,
        string? CityName,
        DateOnly StartedOn,
        DateOnly? EndedOn,
        string? ChangedByUsername
    );
}
