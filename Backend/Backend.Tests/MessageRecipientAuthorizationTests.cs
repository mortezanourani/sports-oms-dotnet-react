using System.Security.Claims;
using Backend.Controllers;
using Backend.Data;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class MessageRecipientAuthorizationTests
{
    [Fact]
    public async Task MainRoleRecipients_UseSelectedScopeAndExcludeAdminsAndFederationReps()
    {
        await using var context = CreateContext();
        var sender = CreateUser(UserRole.Manager, ScopeLevel.Mastery);
        var provinceUser = CreateUser(UserRole.Manager, ScopeLevel.Province);
        var admin = CreateUser(UserRole.Admin, ScopeLevel.Province);
        var federationRep = CreateUser(UserRole.FederationRep, ScopeLevel.Province);
        context.Users.AddRange(sender, provinceUser, admin, federationRep);
        await context.SaveChangesAsync();

        var controller = CreateController(context, sender.Id);
        var result = Assert.IsType<OkObjectResult>(
            await controller.RecipientOptions("MainRole", "Province"));
        var recipients = Assert.IsAssignableFrom<IEnumerable<RecipientOptionDto>>(result.Value);

        Assert.Equal(
            new[] { provinceUser.Id, admin.Id }.ToHashSet(),
            recipients.Select(recipient => recipient.Id).ToHashSet());
        Assert.IsType<OkObjectResult>(await controller.Send(new MessageSendDto(
            null, "متن پیام", [provinceUser.Id], "MainRole", "Province")));
    }

    [Fact]
    public async Task AdminRecipients_IncludeOwnScopeUsersAndAdminsAcrossScopes()
    {
        await using var context = CreateContext();
        var provinceId = Guid.NewGuid();
        var otherProvinceId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var sender = CreateUser(UserRole.Admin, ScopeLevel.Province, provinceId: provinceId);
        var provinceUser = CreateUser(
            UserRole.Manager, ScopeLevel.Province, provinceId: provinceId);
        var cityUser = CreateUser(
            UserRole.SportsManager, ScopeLevel.City, provinceId: provinceId, cityId: cityId);
        var otherProvinceUser = CreateUser(
            UserRole.Manager, ScopeLevel.Province, provinceId: otherProvinceId);
        var masteryAdmin = CreateUser(UserRole.Admin, ScopeLevel.Mastery);
        var sameProvinceAdmin = CreateUser(
            UserRole.Admin, ScopeLevel.Province, provinceId: provinceId);
        var cityAdmin = CreateUser(
            UserRole.Admin, ScopeLevel.City, provinceId: provinceId, cityId: cityId);
        var federationRep = CreateUser(
            UserRole.FederationRep, ScopeLevel.Province, provinceId: provinceId);
        context.Users.AddRange(
            sender, provinceUser, cityUser, otherProvinceUser,
            masteryAdmin, sameProvinceAdmin, cityAdmin, federationRep);
        await context.SaveChangesAsync();

        var controller = CreateController(context, sender.Id);
        var provinceResult = Assert.IsType<OkObjectResult>(
            await controller.RecipientOptions("MainRole", "Province"));
        var provinceRecipients = Assert.IsAssignableFrom<IEnumerable<RecipientOptionDto>>(provinceResult.Value)
            .Select(recipient => recipient.Id)
            .ToHashSet();
        Assert.Contains(provinceUser.Id, provinceRecipients);
        Assert.Contains(sameProvinceAdmin.Id, provinceRecipients);
        Assert.DoesNotContain(otherProvinceUser.Id, provinceRecipients);

        var cityResult = Assert.IsType<OkObjectResult>(
            await controller.RecipientOptions("MainRole", "City"));
        var cityRecipients = Assert.IsAssignableFrom<IEnumerable<RecipientOptionDto>>(cityResult.Value)
            .Select(recipient => recipient.Id)
            .ToHashSet();
        Assert.Contains(cityUser.Id, cityRecipients);
        Assert.Contains(cityAdmin.Id, cityRecipients);

        var masteryResult = Assert.IsType<OkObjectResult>(
            await controller.RecipientOptions("MainRole", "Mastery"));
        var masteryRecipients = Assert.IsAssignableFrom<IEnumerable<RecipientOptionDto>>(masteryResult.Value)
            .Select(recipient => recipient.Id)
            .ToHashSet();
        Assert.Contains(masteryAdmin.Id, masteryRecipients);

        Assert.IsType<OkObjectResult>(await controller.Send(new MessageSendDto(
            null, "متن پیام", [cityUser.Id, cityAdmin.Id], "MainRole", "City")));
        Assert.IsType<BadRequestObjectResult>(await controller.Send(new MessageSendDto(
            null, "متن پیام", [otherProvinceUser.Id], "MainRole", "Province")));
        Assert.DoesNotContain(sender.Id, provinceRecipients);
        Assert.DoesNotContain(federationRep.Id, provinceRecipients);
    }

    [Fact]
    public async Task MainRoleSender_FederationRepRecipientsAreRestrictedToOwnGeographicScopeLevel()
    {
        await using var context = CreateContext();
        var province = new Province { Id = Guid.NewGuid(), Name = "استان اول" };
        var otherProvince = new Province { Id = Guid.NewGuid(), Name = "استان دوم" };
        var city = new City { Id = Guid.NewGuid(), Name = "شهر اول", Province = province };
        var provincialFederation = new Federation
        {
            Id = Guid.NewGuid(),
            Name = "فدراسیون",
            Level = FederationLevel.Provincial,
            Province = province
        };
        var cityFederation = new Federation
        {
            Id = Guid.NewGuid(),
            Name = "فدراسیون شهری",
            Level = FederationLevel.City,
            City = city
        };
        var otherProvinceFederation = new Federation
        {
            Id = Guid.NewGuid(),
            Name = "فدراسیون استان دیگر",
            Level = FederationLevel.Provincial,
            Province = otherProvince
        };
        var sender = CreateUser(
            UserRole.SportsManager, ScopeLevel.Province, provinceId: province.Id);
        var provincialRep = CreateUser(
            UserRole.FederationRep, ScopeLevel.Province, provinceId: province.Id);
        var cityRep = CreateUser(
            UserRole.FederationRep, ScopeLevel.City, provinceId: province.Id, cityId: city.Id);
        var otherProvinceRep = CreateUser(
            UserRole.FederationRep, ScopeLevel.Province, provinceId: otherProvince.Id);
        var nonPresidentRep = CreateUser(
            UserRole.FederationRep, ScopeLevel.Province, provinceId: province.Id);
        context.AddRange(
            province, otherProvince, city, provincialFederation, cityFederation,
            otherProvinceFederation, sender, provincialRep, cityRep, otherProvinceRep,
            nonPresidentRep);
        context.FederationPositions.AddRange(
            ActivePosition(provincialFederation, provincialRep, FederationPositionType.President),
            ActivePosition(cityFederation, cityRep, FederationPositionType.President),
            ActivePosition(otherProvinceFederation, otherProvinceRep),
            ActivePosition(provincialFederation, nonPresidentRep));
        await context.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(
            await CreateController(context, sender.Id).RecipientOptions("FederationRep"));
        var recipients = Assert.IsAssignableFrom<IEnumerable<RecipientOptionDto>>(result.Value);

        Assert.Equal(
            new[] { cityRep.Id, provincialRep.Id }.OrderBy(id => id),
            recipients.Select(recipient => recipient.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task FederationPresident_CanMessageOwnScopeAdminManagerAndSportsManager()
    {
        await using var context = CreateContext();
        var provinceId = Guid.NewGuid();
        var otherProvinceId = Guid.NewGuid();
        var sender = CreateUser(
            UserRole.FederationRep, ScopeLevel.Province, provinceId: provinceId);
        var federation = new Federation
        {
            Id = Guid.NewGuid(),
            Level = FederationLevel.Provincial,
            ProvinceId = provinceId
        };
        var admin = CreateUser(
            UserRole.Admin, ScopeLevel.Province, provinceId: provinceId);
        var manager = CreateUser(
            UserRole.Manager, ScopeLevel.Province, provinceId: provinceId);
        var sportsManager = CreateUser(
            UserRole.SportsManager, ScopeLevel.Province, provinceId: provinceId);
        var cityManager = CreateUser(
            UserRole.Manager, ScopeLevel.City, provinceId: provinceId, cityId: Guid.NewGuid());
        var otherProvinceAdmin = CreateUser(
            UserRole.Admin, ScopeLevel.Province, provinceId: otherProvinceId);
        var sportsOfficer = CreateUser(
            UserRole.SportsOfficer, ScopeLevel.Province, provinceId: provinceId);
        context.AddRange(
            federation, sender, admin, manager, sportsManager,
            cityManager, otherProvinceAdmin, sportsOfficer);
        context.FederationPositions.Add(ActivePosition(
            federation, sender, FederationPositionType.President));
        await context.SaveChangesAsync();

        var controller = CreateController(context, sender.Id);
        var result = Assert.IsType<OkObjectResult>(
            await controller.RecipientOptions("MainRole"));
        var recipients = Assert.IsAssignableFrom<IEnumerable<RecipientOptionDto>>(result.Value)
            .Select(recipient => recipient.Id)
            .ToHashSet();

        Assert.Contains(admin.Id, recipients);
        Assert.Contains(manager.Id, recipients);
        Assert.Contains(sportsManager.Id, recipients);
        Assert.DoesNotContain(cityManager.Id, recipients);
        Assert.DoesNotContain(otherProvinceAdmin.Id, recipients);
        Assert.DoesNotContain(sportsOfficer.Id, recipients);

        Assert.IsType<OkObjectResult>(await controller.Send(new MessageSendDto(
            null, "متن پیام", [admin.Id, manager.Id, sportsManager.Id], "MainRole", null)));
    }

    [Fact]
    public async Task FederationRepRecipients_AreSameFederationAndAdjacentPresidentsOnly()
    {
        await using var context = CreateContext();
        var province = new Province { Id = Guid.NewGuid(), Name = "استان اول" };
        var otherProvince = new Province { Id = Guid.NewGuid(), Name = "استان دوم" };
        var city = new City { Id = Guid.NewGuid(), Name = "شهر اول", Province = province };
        var nationalFederation = new Federation
        {
            Id = Guid.NewGuid(), Name = "ملی", Level = FederationLevel.National
        };
        var provinceFederation = new Federation
        {
            Id = Guid.NewGuid(),
            Level = FederationLevel.Provincial,
            ParentFederation = nationalFederation,
            Province = province
        };
        var cityFederation = new Federation
        {
            Id = Guid.NewGuid(),
            Level = FederationLevel.City,
            ParentFederation = provinceFederation,
            City = city
        };
        var unrelatedProvinceFederation = new Federation
        {
            Id = Guid.NewGuid(),
            Level = FederationLevel.Provincial,
            ParentFederation = nationalFederation,
            Province = otherProvince
        };
        var sender = CreateUser(
            UserRole.FederationRep, ScopeLevel.Province, provinceId: province.Id);
        var sameFederationRep = CreateUser(UserRole.FederationRep, ScopeLevel.Province);
        var unrelatedSameLevelRep = CreateUser(UserRole.FederationRep, ScopeLevel.Province);
        var nationalPresident = CreateUser(UserRole.FederationRep, ScopeLevel.Mastery);
        var cityPresident = CreateUser(UserRole.FederationRep, ScopeLevel.City);
        var unrelatedPresident = CreateUser(UserRole.FederationRep, ScopeLevel.Province);
        context.AddRange(
            province, otherProvince, city, nationalFederation, provinceFederation,
            cityFederation, unrelatedProvinceFederation, sender, sameFederationRep,
            unrelatedSameLevelRep, nationalPresident, cityPresident, unrelatedPresident);
        context.FederationPositions.AddRange(
            ActivePosition(provinceFederation, sender, FederationPositionType.President),
            ActivePosition(provinceFederation, sameFederationRep),
            ActivePosition(unrelatedProvinceFederation, unrelatedSameLevelRep),
            ActivePosition(nationalFederation, nationalPresident, FederationPositionType.President),
            ActivePosition(cityFederation, cityPresident, FederationPositionType.President),
            ActivePosition(unrelatedProvinceFederation, unrelatedPresident, FederationPositionType.President));
        await context.SaveChangesAsync();

        var controller = CreateController(context, sender.Id);
        var result = Assert.IsType<OkObjectResult>(
            await controller.RecipientOptions("FederationRep"));
        var recipients = Assert.IsAssignableFrom<IEnumerable<RecipientOptionDto>>(result.Value)
            .Select(recipient => recipient.Id)
            .ToHashSet();

        Assert.Contains(sameFederationRep.Id, recipients);
        Assert.Contains(nationalPresident.Id, recipients);
        Assert.Contains(cityPresident.Id, recipients);
        Assert.DoesNotContain(unrelatedSameLevelRep.Id, recipients);
        Assert.DoesNotContain(unrelatedPresident.Id, recipients);

        var rejected = Assert.IsType<BadRequestObjectResult>(await controller.Send(new MessageSendDto(
            null, "متن پیام", [unrelatedSameLevelRep.Id], "FederationRep", null)));
        Assert.NotNull(rejected.Value);
        Assert.IsType<OkObjectResult>(await controller.Send(new MessageSendDto(
            null, "متن پیام", [sameFederationRep.Id, nationalPresident.Id, cityPresident.Id],
            "FederationRep", null)));
    }

    [Theory]
    [InlineData(UserRole.SportsManager, true)]
    [InlineData(UserRole.SportsOfficer, false)]
    [InlineData(UserRole.M5Manager, true)]
    [InlineData(UserRole.M5Officer, false)]
    [InlineData(UserRole.M88Manager, true)]
    [InlineData(UserRole.M88Officer, false)]
    [InlineData(UserRole.HumanResourceManager, true)]
    [InlineData(UserRole.HumanResourceOfficer, false)]
    [InlineData(UserRole.InsuranceOfficer, true)]
    public async Task RoleFamilies_CrossScopeMessagingIsLimitedToTheirPeerRole(
        UserRole senderRole, bool canMessagePeerAtAdjacentScope)
    {
        await using var context = CreateContext();
        var provinceId = Guid.NewGuid();
        var sender = CreateUser(
            senderRole, ScopeLevel.Province, provinceId: provinceId);
        var peerAtMastery = CreateUser(senderRole, ScopeLevel.Mastery);
        var otherRole = senderRole == UserRole.M5Manager
            ? UserRole.M88Manager
            : UserRole.M5Manager;
        var otherRoleAtMastery = CreateUser(otherRole, ScopeLevel.Mastery);
        context.Users.AddRange(sender, peerAtMastery, otherRoleAtMastery);
        await context.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(
            await CreateController(context, sender.Id)
                .RecipientOptions("MainRole", "Mastery"));
        var recipients = Assert.IsAssignableFrom<IEnumerable<RecipientOptionDto>>(result.Value)
            .Select(recipient => recipient.Id)
            .ToHashSet();

        Assert.Equal(canMessagePeerAtAdjacentScope, recipients.Contains(peerAtMastery.Id));
        Assert.DoesNotContain(otherRoleAtMastery.Id, recipients);
    }

    [Fact]
    public async Task Officers_CanMessageSameProvinceAndCityUsersButNotManagersOrAdmins()
    {
        await using var context = CreateContext();
        var provinceId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var sender = CreateUser(
            UserRole.SportsOfficer, ScopeLevel.Province, provinceId: provinceId);
        var cityOfficer = CreateUser(
            UserRole.SportsOfficer, ScopeLevel.City, provinceId: provinceId, cityId: cityId);
        var cityAdmin = CreateUser(
            UserRole.Admin, ScopeLevel.City, provinceId: provinceId, cityId: cityId);
        var cityManager = CreateUser(
            UserRole.M5Manager, ScopeLevel.City, provinceId: provinceId, cityId: cityId);
        var cityUser = CreateUser(
            UserRole.HumanResourceOfficer, ScopeLevel.City, provinceId: provinceId, cityId: cityId);
        context.Users.AddRange(sender, cityOfficer, cityAdmin, cityManager, cityUser);
        await context.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(
            await CreateController(context, sender.Id)
                .RecipientOptions("MainRole", "City"));
        var recipients = Assert.IsAssignableFrom<IEnumerable<RecipientOptionDto>>(result.Value)
            .Select(recipient => recipient.Id)
            .ToHashSet();

        Assert.Contains(cityOfficer.Id, recipients);
        Assert.Contains(cityUser.Id, recipients);
        Assert.DoesNotContain(cityAdmin.Id, recipients);
        Assert.DoesNotContain(cityManager.Id, recipients);
    }

    private static FederationPosition ActivePosition(
        Federation federation,
        ApplicationUser user,
        FederationPositionType position = FederationPositionType.Miss) => new()
    {
        Id = Guid.NewGuid(),
        Federation = federation,
        User = user,
        Position = position,
        StartDate = DateTime.UtcNow.AddDays(-30),
        EndDate = DateTime.UtcNow.AddDays(30)
    };

    private static ApplicationUser CreateUser(
        UserRole role,
        ScopeLevel scopeLevel,
        Guid? provinceId = null,
        Guid? cityId = null) => new()
    {
        Id = Guid.NewGuid(),
        UserName = Guid.NewGuid().ToString(),
        Role = role,
        ScopeLevel = scopeLevel,
        ScopeProvinceId = provinceId,
        ScopeCityId = cityId
    };

    private static MessagesController CreateController(ApplicationDbContext context, Guid userId) =>
        new(context, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(AuthClaimTypes.Role, "Test")
                    ], "test"))
                }
            }
        };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
