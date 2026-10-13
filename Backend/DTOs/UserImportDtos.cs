namespace Backend.DTOs;

public record UserImportRowError(int RowNumber, string Reason);

public record UserImportResultDto(
    int Created,
    int SkippedExisting,
    int SkippedRequired,
    int Failed,
    IReadOnlyList<UserImportRowError> Errors);
