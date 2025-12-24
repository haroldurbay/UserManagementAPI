using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserManagementAPI.Models;

namespace UserManagementAPI.Services
{
    public class UserFileRepository : IUserRepository
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _mutex = new(1, 1);
        private readonly ILogger<UserFileRepository> _logger;
        private List<UserDto> _cache = new();
        private bool _loaded;

        public UserFileRepository(IHostEnvironment env, ILogger<UserFileRepository> logger)
        {
            var dataDir = Path.Combine(env.ContentRootPath, "data");
            Directory.CreateDirectory(dataDir);
            _filePath = Path.Combine(dataDir, "users.json");

            _logger = logger;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<(bool ok, IReadOnlyList<UserDto> users, string? error)> GetAllAsync()
        {
            await _mutex.WaitAsync();
            try
            {
                var load = await LoadUsersSafeAsync();
                return (load.ok, load.users, load.error);
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async Task<(bool ok, UserDto? user, string? error)> GetByIdAsync(Guid id)
        {
            await _mutex.WaitAsync();
            try
            {
                var load = await LoadUsersSafeAsync();
                if (!load.ok) return (false, null, load.error);
                var user = load.users.FirstOrDefault(u => u.Id == id);
                return (true, user, null);
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async Task<(bool created, UserDto? user, string? error)> CreateAsync(CreateUserDto dto)
        {
            await _mutex.WaitAsync();
            try
            {
                if (!TryValidate(dto, out var validationError))
                    return (false, null, validationError);

                var load = await LoadUsersSafeAsync();
                if (!load.ok) return (false, null, load.error);
                var users = load.users;
                if (users.Any(u => u.Email.Equals(dto.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    return (false, null, "Email already exists.");
                }

                var user = new UserDto(Guid.NewGuid(), dto.FirstName, dto.LastName, dto.Email);
                users.Add(user);
                var saved = await SaveUsersSafeAsync(users);
                if (!saved.ok) return (false, null, saved.error);
                return (true, user, null);
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async Task<(bool updated, string? error)> UpdateAsync(Guid id, UpdateUserDto dto)
        {
            await _mutex.WaitAsync();
            try
            {
                if (!TryValidate(dto, out var validationError))
                    return (false, validationError);

                var load = await LoadUsersSafeAsync();
                if (!load.ok) return (false, load.error);
                var users = load.users;
                var idx = users.FindIndex(u => u.Id == id);
                if (idx == -1)
                {
                    return (false, "not-found");
                }

                if (users.Any(u => u.Id != id && u.Email.Equals(dto.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    return (false, "duplicate-email");
                }

                users[idx] = new UserDto(id, dto.FirstName, dto.LastName, dto.Email);
                var saved = await SaveUsersSafeAsync(users);
                if (!saved.ok) return (false, saved.error);
                return (true, null);
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async Task<(bool deleted, string? error)> DeleteAsync(Guid id)
        {
            await _mutex.WaitAsync();
            try
            {
                var load = await LoadUsersSafeAsync();
                if (!load.ok) return (false, load.error);
                var users = load.users;
                var removed = users.RemoveAll(u => u.Id == id) > 0;
                if (!removed) return (false, null);
                var saved = await SaveUsersSafeAsync(users);
                if (!saved.ok) return (false, saved.error);
                return (true, null);
            }
            finally
            {
                _mutex.Release();
            }
        }

        private async Task<(bool ok, List<UserDto> users, string? error)> LoadUsersSafeAsync()
        {
            try
            {
                if (_loaded)
                {
                    return (true, _cache, null);
                }

                if (!File.Exists(_filePath)) return (true, new(), null);
                var json = await File.ReadAllTextAsync(_filePath);
                if (string.IsNullOrWhiteSpace(json)) return (true, new(), null);
                var users = JsonSerializer.Deserialize<List<UserDto>>(json, _jsonOptions) ?? new();
                _cache = users;
                _loaded = true;
                return (true, _cache, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load users from {FilePath}", _filePath);
                return (false, new(), "store-unavailable");
            }
        }

        private async Task<(bool ok, string? error)> SaveUsersSafeAsync(List<UserDto> users)
        {
            try
            {
                var json = JsonSerializer.Serialize(users, _jsonOptions);
                await File.WriteAllTextAsync(_filePath, json);
                _cache = users;
                _loaded = true;
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save users to {FilePath}", _filePath);
                return (false, "store-write-failed");
            }
        }

        private static bool TryValidate<T>(T dto, out string? error)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(dto!);
            if (Validator.TryValidateObject(dto!, context, results, validateAllProperties: true))
            {
                error = null;
                return true;
            }

            error = string.Join("; ", results.Select(r => r.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)))
                .Trim();
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "validation failed";
            }
            error = $"validation: {error}";
            return false;
        }
    }
}
