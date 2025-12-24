using System;
using System.ComponentModel.DataAnnotations;

namespace UserManagementAPI.Models
{
    /// <summary>
    /// Represents a user returned by the API.
    /// </summary>
    /// <param name="Id">Unique identifier for the user.</param>
    /// <param name="FirstName">Given name.</param>
    /// <param name="LastName">Family name.</param>
    /// <param name="Email">Email address (unique per user).</param>
    public record UserDto(Guid Id, string FirstName, string LastName, string Email);

    public class CreateUserDto
    {
        /// <summary>Given name.</summary>
        [Required, StringLength(100)]
        public string FirstName { get; init; } = string.Empty;

        /// <summary>Family name.</summary>
        [Required, StringLength(100)]
        public string LastName { get; init; } = string.Empty;

        /// <summary>Email address (must be unique and valid format).</summary>
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; init; } = string.Empty;
    }

    public class UpdateUserDto
    {
        /// <summary>Given name.</summary>
        [Required, StringLength(100)]
        public string FirstName { get; init; } = string.Empty;

        /// <summary>Family name.</summary>
        [Required, StringLength(100)]
        public string LastName { get; init; } = string.Empty;

        /// <summary>Email address (must be unique and valid format).</summary>
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; init; } = string.Empty;
    }
}
