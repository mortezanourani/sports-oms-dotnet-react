namespace Backend.DTOs;

public record AthleteCountImportRowError(int RowNumber, string Reason);

public record AthleteCountImportResultDto(
    int Created,
    int SkippedInvalid,
    int SkippedOutOfScope,
    int SkippedExisting,
    IReadOnlyList<AthleteCountImportRowError> Errors);
