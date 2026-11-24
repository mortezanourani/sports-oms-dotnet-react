namespace Backend.DTOs
{
    public record OrgUnitUpsertDto(
        string Title,
        string Type,
        string Scope,
        Guid? ParentOrgUnitId);

    public record OrgUnitOutDto(
        Guid Id,
        string Title,
        string Type,
        string Scope,
        Guid? ParentOrgUnitId,
        string? ParentOrgUnitTitle);

    public record OrgUnitImportErrorDto(int RowNumber, string Reason);

    public record OrgUnitImportResultDto(
        int Created,
        int SkippedExisting,
        int Failed,
        IReadOnlyList<OrgUnitImportErrorDto> Errors);
}
