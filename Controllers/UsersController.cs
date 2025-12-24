using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UserManagementAPI.Models;
using UserManagementAPI.Services;

namespace UserManagementAPI.Controllers
{
    /// <summary>
    /// Manages users with basic CRUD operations using an in-memory store.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _repo;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserRepository repo, ILogger<UsersController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        /// <summary>Get users with optional pagination.</summary>
        [HttpGet]
        public async Task<ActionResult<object>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            const int maxPageSize = 200;
            if (page <= 0 || pageSize <= 0 || pageSize > maxPageSize)
                return BadRequest(new { message = $"page must be >= 1 and pageSize must be between 1 and {maxPageSize}." });

            try
            {
                var result = await _repo.GetAllAsync();
                if (!result.ok)
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "User store unavailable." });

                var total = result.users.Count;
                var items = result.users
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var totalPages = (int)Math.Ceiling(total / (double)pageSize);

                return Ok(new
                {
                    page,
                    pageSize,
                    total,
                    totalPages,
                    items
                });
            }
            catch (Exception ex)
            {
                return UnexpectedError(ex, "GetAll");
            }
        }

        /// <summary>Get a single user by id.</summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UserDto>> GetById(Guid id)
        {
            try
            {
                var result = await _repo.GetByIdAsync(id);
                if (!result.ok)
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "User store unavailable." });

                return result.user is null
                    ? NotFound(new { message = "User not found." })
                    : Ok(result.user);
            }
            catch (Exception ex)
            {
                return UnexpectedError(ex, "GetById", id);
            }
        }

        /// <summary>Create a new user.</summary>
        [HttpPost]
        public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            try
            {
                var result = await _repo.CreateAsync(dto);

                if (!result.created)
                {
                    if (result.error != null && result.error.StartsWith("validation:"))
                        return BadRequest(new { message = result.error["validation:".Length..].Trim() });

                    if (result.error == "store-unavailable" || result.error == "store-write-failed")
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "User store unavailable." });

                    return Conflict(new { message = result.error ?? "Unable to create user." });
                }

                return CreatedAtAction(nameof(GetById), new { id = result.user!.Id }, result.user);
            }
            catch (Exception ex)
            {
                return UnexpectedError(ex, "Create");
            }
        }

        /// <summary>Update an existing user.</summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            try
            {
                var result = await _repo.UpdateAsync(id, dto);

                if (!result.updated && result.error != null && result.error.StartsWith("validation:"))
                    return BadRequest(new { message = result.error["validation:".Length..].Trim() });

                if (!result.updated && (result.error == "store-unavailable" || result.error == "store-write-failed"))
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "User store unavailable." });

                if (!result.updated && result.error == "not-found")
                    return NotFound(new { message = "User not found." });

                if (!result.updated && result.error == "duplicate-email")
                    return Conflict(new { message = "Email already exists." });

                if (!result.updated)
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to update user." });

                return NoContent();
            }
            catch (Exception ex)
            {
                return UnexpectedError(ex, "Update", id);
            }
        }

        /// <summary>Delete a user by id.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _repo.DeleteAsync(id);

                if (!result.deleted && (result.error == "store-unavailable" || result.error == "store-write-failed"))
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "User store unavailable." });

                if (!result.deleted) return NotFound(new { message = "User not found." });
                return NoContent();
            }
            catch (Exception ex)
            {
                return UnexpectedError(ex, "Delete", id);
            }
        }

        private ActionResult UnexpectedError(Exception ex, string operation, Guid? id = null)
        {
            if (id.HasValue)
                _logger.LogError(ex, "Unhandled exception in {Operation} for {UserId}", operation, id.Value);
            else
                _logger.LogError(ex, "Unhandled exception in {Operation}", operation);

            var payload = new
            {
                error = "Internal server error.",
                traceId = HttpContext.TraceIdentifier
            };

            return StatusCode(StatusCodes.Status500InternalServerError, payload);
        }
    }
}
