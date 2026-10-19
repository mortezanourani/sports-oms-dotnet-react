namespace Backend.DTOs
{
    // Deliberately excludes OwnerName/OwnerContact/Address details beyond
    // city-level — a public visitor should be able to see "how many sports
    // facilities exist in Tehran," not a specific facility's full street
    // address or the private owner's contact info.
    public record PublicFacilityStatDto(
        string FacilityType, string ProvinceName, string CityName, int Count);

    public record PublicChampionDto(
        Guid Id, string FullName, string Gender, string FederationName,
        string? CityName, string? PhotoUrl, int MedalCount,
        int GoldCount, int SilverCount, int BronzeCount);

    public record PublicAthleteStatDto(
        string FederationName, string ProvinceName, string CityName,
        int Year, int Month, int MaleCount, int FemaleCount);
}
