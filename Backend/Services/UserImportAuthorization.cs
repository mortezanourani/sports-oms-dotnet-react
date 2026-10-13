using Backend.Models;

namespace Backend.Services;

public static class UserImportAuthorization
{
    public static bool CanAssignRole(UserRole uploaderRole, UserRole targetRole)
    {
        if (targetRole == UserRole.FederationRep)
            return false;

        return uploaderRole switch
        {
            UserRole.Admin => true,
            UserRole.Manager => targetRole != UserRole.Admin,
            UserRole.HumanResourceManager or UserRole.HumanResourceOfficer =>
                targetRole is not (UserRole.Admin or UserRole.Manager),
            _ => false
        };
    }
}
