namespace CampusEats.Api.Dtos;

public record MenuItemDto(
    int Id, string Name, decimal Price,
    string Category, bool Available);