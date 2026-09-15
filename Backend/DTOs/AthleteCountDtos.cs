namespace Backend.DTOs
{
    public record AthleteCountCreateDto(
        Guid FederationId, Guid CityId, int Year, int Month, string Gender, int Count);

    public record AthleteCountUpdateDto(int Count);
    // Update only allows changing the Count itself — the identity of a record
    // (which federation, city, year, month, gender) shouldn't change via edit.
    // If that combination was wrong, the correct fix is deleting the wrong row
    // and creating a new correct one, not silently reassigning an existing
    // row's identity, which would corrupt the audit trail of "what was
    // recorded for period X" if X itself could be edited after the fact.

    public record AthleteCountOutDto(
        Guid Id, Guid FederationId, string FederationName,
        Guid CityId, string CityName, string ProvinceName,
        int Year, int Month, string Gender, int Count);
}
