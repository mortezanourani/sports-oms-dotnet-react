namespace Backend.DTOs
{
    public record JobPostImportErrorDto(int RowNumber, string Reason);

    public record JobPostImportResultDto(
        int Created,
        int SkippedExisting,
        int Failed,
        IReadOnlyList<JobPostImportErrorDto> Errors);
}
