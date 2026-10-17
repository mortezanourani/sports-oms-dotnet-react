namespace Backend.DTOs;

public record MsyGovFacilityImportRowError(int RowNumber, string Reason);

public record MsyGovFacilityImportResultDto(
    int CreatedFacilities,
    int ExistingFacilities,
    int CreatedContracts,
    int SkippedExistingContracts,
    int SkippedInvalid,
    int SkippedInvalidContracts,
    int SkippedOutOfScope,
    IReadOnlyList<MsyGovFacilityImportRowError> Errors);
