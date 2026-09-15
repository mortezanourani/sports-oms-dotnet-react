namespace Backend.DTOs
{
    public record FederationCreateDto(string Name, string SportType);

    public record FederationUpdateDto(string Name, string SportType);

    public record FederationOutDto(Guid Id, string Name, string SportType);
}
