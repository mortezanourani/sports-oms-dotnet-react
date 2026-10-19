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

public class FederationCategoryTests
{
    [Fact]
    public void FederationCategoryIsOptionalInTheDatabaseModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;
        using var context = new ApplicationDbContext(options);

        var category = context.Model.FindEntityType(typeof(Federation))!
            .FindProperty(nameof(Federation.Category))!;

        Assert.True(category.IsNullable);
    }

    [Fact]
    public async Task UpdateNationalFederationCategory_UpdatesAllDescendants()
    {
        await using var context = CreateContext();
        var national = new Federation
        {
            Id = Guid.NewGuid(),
            Name = "فدراسیون نمونه",
            Level = FederationLevel.National,
            Category = FederationCategory.General
        };
        var provincial = new Federation
        {
            Id = Guid.NewGuid(),
            Level = FederationLevel.Provincial,
            ParentFederationId = national.Id,
            Category = FederationCategory.General
        };
        var city = new Federation
        {
            Id = Guid.NewGuid(),
            Level = FederationLevel.City,
            ParentFederationId = provincial.Id,
            Category = FederationCategory.General
        };
        var unrelated = new Federation
        {
            Id = Guid.NewGuid(),
            Name = "فدراسیون دیگر",
            Level = FederationLevel.National,
            Category = FederationCategory.Para
        };
        context.Federations.AddRange(national, provincial, city, unrelated);
        await context.SaveChangesAsync();

        var controller = new FederationsController(
            context,
            null!,
            new FederationPositionService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = MasteryPrincipal() }
            }
        };

        var result = await controller.Update(national.Id, new FederationUpdateDto(
            national.Name, FederationCategory.Championships.ToString()));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(FederationCategory.Championships, (await context.Federations.FindAsync(national.Id))!.Category);
        Assert.Equal(FederationCategory.Championships, (await context.Federations.FindAsync(provincial.Id))!.Category);
        Assert.Equal(FederationCategory.Championships, (await context.Federations.FindAsync(city.Id))!.Category);
        Assert.Equal(FederationCategory.Para, (await context.Federations.FindAsync(unrelated.Id))!.Category);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ClaimsPrincipal MasteryPrincipal() => new(
        new ClaimsIdentity(
        [
            new Claim(AuthClaimTypes.Role, "Admin"),
            new Claim(AuthClaimTypes.ScopeLevel, "Mastery")
        ],
        authenticationType: "test"));
}
