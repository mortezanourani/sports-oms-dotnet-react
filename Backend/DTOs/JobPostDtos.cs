namespace Backend.DTOs
{
    public record JobPostUpsertDto(
        Guid JobPostTypeId,
        Guid OrgUnitId,
        string TypeCode,
        string Title,
        string ManagerType,
        bool? BreakOut
    );

    public record JobPostOutDto(
        Guid Id,
        Guid JobPostTypeId,
        Guid OrgUnitId,
        string TypeCode,
        string Title,
        string ManagerType,
        bool? BreakOut
    );
}
