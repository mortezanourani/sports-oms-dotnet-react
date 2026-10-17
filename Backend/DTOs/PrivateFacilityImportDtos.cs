namespace Backend.DTOs;

public record PrivateFacilityImportRowError(int RowNumber, string Reason);

public record PrivateFacilityImportResultDto(
    int CreatedFacilities,
    int ExistingFacilities,
    int CreatedLicenses,
    int SkippedExistingLicenses,
    int SkippedInvalidFacilities,
    int SkippedInvalidLicenses,
    int SkippedOutOfScope,
    IReadOnlyList<PrivateFacilityImportRowError> Errors);
