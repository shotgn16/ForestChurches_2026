namespace ForestChurches.Components.Permissions
{
    public class AppPermissions
    {
        public class Admin
        {
            public const string Read = "Admin.Read";
        }
        public class EventManagement
        {
            public const string Read = "EventManagement.Read";
            public const string Write = "EventManagement.Write";
            public const string Edit = "EventManagement.Edit";
        }
        public class RoleManagement
        {
            public const string Read = "RoleManagement.Read";
            public const string Write = "RoleManagement.Write";
        }
        public class UserEvents
        {
            public const string Read = "UserEvents.View";
            public const string Edit = "UserEvents.Edit";
            public const string Add = "UserEvents.Add";
        }
        public class UserManagement
        {
            public const string Read = "UserManagement.Read";
            public const string Write = "UserManagement.Write";
            public const string Edit = "UserManagement.Edit";
        }

        public class User
        {
            public const string View = "User.View";
        }
    }
}
