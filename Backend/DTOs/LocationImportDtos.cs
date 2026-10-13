namespace Backend.DTOs;

public record LocationImportRowError(int RowNumber, string Reason);

public record LocationImportResultDto(
    int CreatedProvinces,
    int CreatedCities,
    int SkippedExisting,
    int SkippedMissingProvince,
    int SkippedInvalidProvince,
    int Failed,
    IReadOnlyList<LocationImportRowError> Errors);
