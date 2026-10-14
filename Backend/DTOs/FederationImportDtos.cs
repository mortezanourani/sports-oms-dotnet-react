namespace Backend.DTOs;

public record FederationImportRowError(int RowNumber, string Reason);

public record FederationImportResultDto(
    int CreatedFederations,
    int SkippedExistingFederations,
    int CreatedRepAccounts,
    int AssignedPositions,
    int SkippedRepAssignments,
    int SkippedInvalid,
    int SkippedOutOfScope,
    IReadOnlyList<FederationImportRowError> Errors);
