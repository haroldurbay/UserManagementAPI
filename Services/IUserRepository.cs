using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UserManagementAPI.Models;

namespace UserManagementAPI.Services
{
    public interface IUserRepository
    {
        Task<(bool ok, IReadOnlyList<UserDto> users, string? error)> GetAllAsync();
        Task<(bool ok, UserDto? user, string? error)> GetByIdAsync(Guid id);
        Task<(bool created, UserDto? user, string? error)> CreateAsync(CreateUserDto dto);
        Task<(bool updated, string? error)> UpdateAsync(Guid id, UpdateUserDto dto);
        Task<(bool deleted, string? error)> DeleteAsync(Guid id);
    }
}
