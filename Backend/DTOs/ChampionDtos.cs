namespace Backend.DTOs
{
    public record ChampionCreateDto(
        string FullName, DateOnly? BirthDate, string Gender,
        Guid FederationId, Guid? CityId, string? PhotoPath);

    public record ChampionUpdateDto(
        string FullName, DateOnly? BirthDate, string Gender,
        Guid FederationId, Guid? CityId, string? PhotoPath);

    // Used in list views — deliberately excludes the full Medals collection,
    // since a list of 100 champions each carrying their full medal history
    // would be a heavy response for a page that's just meant to show names
    // and basic info in a table.
    public record ChampionListOutDto(
        Guid Id, string FullName, DateOnly? BirthDate, string Gender,
        Guid FederationId, string FederationName,
        Guid? CityId, string? CityName, int MedalCount);

    // Used for a single champion's detail view — this is where the full
    // medal history actually belongs, since that's exactly the page where
    // a user wants to see it.
    public record ChampionDetailOutDto(
        Guid Id, string FullName, DateOnly? BirthDate, string Gender,
        Guid FederationId, string FederationName,
        Guid? CityId, Guid? ProvinceId, string? CityName, string? ProvinceName,
        string? PhotoPath, List<MedalOutDto> Medals);

    public record MedalCreateDto(
        string MedalType, string AgeGroup, string Level,
        string CompetitionName, int Year, int Count, string? Field);

    public record MedalOutDto(
        Guid Id, string MedalType, string AgeGroup, string Level,
        string CompetitionName, int Year, int Count, string? Field);
}
