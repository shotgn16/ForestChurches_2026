using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;

namespace ForestChurches.Components.Roles
{
    public class ChurchRoles : IdentityRole<string>
    {
        public ChurchRoles() : base()
        {
            Id = Guid.NewGuid().ToString();
        }

        public ChurchRoles(string roleName) : base(roleName)
        {
            Id = Guid.NewGuid().ToString();
        }

        public List<RolePermission> RolePermissions { get; set; }
    }
}