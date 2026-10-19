namespace Backend.DTOs
{
    public record AthleteCountCreateDto(
        Guid FederationId, Guid CityId, int Year, int Month, int MaleCount, int FemaleCount);

    public record AthleteCountUpdateDto(int MaleCount, int FemaleCount);

    public record AthleteCountOutDto(
        Guid Id, Guid FederationId, string FederationName,
        Guid CityId, string CityName, string ProvinceName,
        int Year, int Month, int MaleCount, int FemaleCount);

    public record AthleteCountFederationSummaryDto(
        Guid FederationId, string FederationName, Guid? RecordId,
        int MaleCount, int FemaleCount, int TotalCount);

    public record AthleteCountSummaryFiltersDto(int[] Years, int[] Months);
}
