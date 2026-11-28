namespace Backend.DTOs
{
    public record JobPostUpsertDto(
        Guid JobPostTypeId,
        Guid OrgUnitId,
        string TypeCode,
        string JobPostCode,
        string JobPostUniqueId,
        string Title,
        string ManagerType,
        bool? BreakOut
    );

    public record JobPostOutDto(
        Guid Id,
        Guid JobPostTypeId,
        Guid OrgUnitId,
        string TypeCode,
        string JobPostCode,
        string JobPostUniqueId,
        string Title,
        string ManagerType,
        bool? BreakOut
    );
}
