namespace Backend.DTOs
{
    public record FacilityCreateDto(
        string FacilityType, string Name, string Address, string SportType,
        string? OwnerName, string? OwnerContact, Guid CityId);

    public record FacilityUpdateDto(
        string FacilityType, string Name, string Address, string SportType,
        string? OwnerName, string? OwnerContact, Guid CityId);

    public record FacilityOutDto(
        Guid Id, string FacilityType, string Name, string Address, string SportType,
        string? OwnerName, string? OwnerContact, Guid CityId, Guid ProvinceId, string CityName,
        string ProvinceName, DateTime CreatedAt);
}
