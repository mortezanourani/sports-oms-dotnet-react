using Backend.Models;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class UserImportAuthorizationTests
{
    [Theory]
    [InlineData(UserRole.Admin, UserRole.Admin, true)]
    [InlineData(UserRole.Admin, UserRole.Manager, true)]
    [InlineData(UserRole.Admin, UserRole.FederationRep, false)]
    [InlineData(UserRole.Manager, UserRole.Admin, false)]
    [InlineData(UserRole.Manager, UserRole.SportsOfficer, true)]
    [InlineData(UserRole.Manager, UserRole.FederationRep, false)]
    [InlineData(UserRole.HumanResourceManager, UserRole.Admin, false)]
    [InlineData(UserRole.HumanResourceManager, UserRole.Manager, false)]
    [InlineData(UserRole.HumanResourceManager, UserRole.SportsOfficer, true)]
    [InlineData(UserRole.HumanResourceOfficer, UserRole.Manager, false)]
    [InlineData(UserRole.SportsManager, UserRole.SportsOfficer, false)]
    [InlineData(UserRole.FederationRep, UserRole.FederationRep, false)]
    public void CanAssignRole_EnforcesUploaderAndTargetRolePolicy(
        UserRole uploaderRole,
        UserRole targetRole,
        bool expected)
    {
        Assert.Equal(expected, UserImportAuthorization.CanAssignRole(uploaderRole, targetRole));
    }
}
