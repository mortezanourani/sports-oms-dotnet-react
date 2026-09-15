namespace Backend.DTOs
{
    public record ProvinceCreateDto(string Name);

    public record ProvinceOutDto(Guid Id, string Name);

    public record CityCreateDto(Guid ProvinceId, string Name);

    public record CityOutDto(Guid Id, string Name, Guid ProvinceId, string ProvinceName);
}
