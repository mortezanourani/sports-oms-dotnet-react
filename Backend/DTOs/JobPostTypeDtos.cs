namespace Backend.DTOs
{
    public record JobPostTypeUpsertDto(string Title);

    public record JobPostTypeOutDto(Guid Id, string Title);

    public record JobPostTypeImportErrorDto(int RowNumber, string Reason);

    public record JobPostTypeImportResultDto(
        int Created,
        int SkippedExisting,
        int Failed,
        IReadOnlyList<JobPostTypeImportErrorDto> Errors);
}
