namespace Backend.DTOs;

public record ChampionImportRowError(int RowNumber, string Reason);

public record ChampionImportResultDto(
    int CreatedChampions,
    int ExistingChampions,
    int CreatedMedals,
    int SkippedExistingMedals,
    int SkippedInvalid,
    int SkippedOutOfScope,
    IReadOnlyList<ChampionImportRowError> Errors);
