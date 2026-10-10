using CampusEats.Api.Dtos;

namespace CampusEats.Api.Services;

public interface IMenuService
{
    IEnumerable<MenuItemDto> GetAll();
    MenuItemDto? GetById(int id);
    MenuItemDto Create(CreateMenuItemDto dto);
    bool Update(int id, CreateMenuItemDto dto);
    bool Delete(int id);
}