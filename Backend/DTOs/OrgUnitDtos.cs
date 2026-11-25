namespace Backend.DTOs
{
    public record OrgUnitUpsertDto(
        string Title,
        string Type,
        string Scope,
        Guid ProvinceId,
        Guid? CityId,
        Guid? ParentOrgUnitId);

    public record OrgUnitOutDto(
        Guid Id,
        string Title,
        string Type,
        string Scope,
        Guid ProvinceId,
        string ProvinceName,
        Guid? CityId,
        string? CityName,
        Guid? ParentOrgUnitId,
        string? ParentOrgUnitTitle);

    public record OrgUnitImportErrorDto(int RowNumber, string Reason);

    public record OrgUnitImportResultDto(
        int Created,
        int SkippedExisting,
        int Failed,
        IReadOnlyList<OrgUnitImportErrorDto> Errors);
}
