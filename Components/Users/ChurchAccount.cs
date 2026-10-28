using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;

namespace ForestChurches.Components.Users;

// Add profile data for application users by adding properties to the  class
public class ChurchAccount : IdentityUser<string>
{
    /// <summary>
    /// The churches this account manages. Usually one; a benefice account can manage several.
    /// Churches are added, moved and removed by site administrators.
    /// </summary>
    public ICollection<ChurchInformation> Churches { get; set; } = new List<ChurchInformation>();

    [PersonalData]
    public string CreatedDate { get; set; }

    [AllowNull]
    [PersonalData]
    [Obsolete("Use ImageBlobName to store image in Azure Blob Storage instead of ImageArray.")]
    public byte[] ImageArray { get; set; }

    [AllowNull]
    [PersonalData]
    public string? ImageBlobName { get; set; }

    [AllowNull]
    [PersonalData]
    public string Role { get; set; }

    [AllowNull]
    [PersonalData]
    public string? ClientId { get; set; }
}

