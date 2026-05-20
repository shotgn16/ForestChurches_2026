using ForestChurches.Components.Roles;

namespace ForestChurches.Models
{
    public class Permission
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<RolePermission> RolePermissions { get; set; }
    }

    public class RolePermission
    {
        public string RoleId { get; set; }
        public string PermissionId { get; set; }
        public ChurchRoles Role { get; set; }
        public string PermissionName { get; set; }
        public Permission Permission { get; set; }
    }
}