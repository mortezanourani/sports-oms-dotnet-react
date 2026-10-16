using System.Reflection;
using System.Security.Claims;
using Backend.Controllers;
using Backend.Data;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class FacilitiesControllerTests
{
    [Fact]
    public void Controller_IsRestrictedToFacilityRoles()
    {
        var authorize = typeof(FacilitiesController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Admin,Manager,M88Manager,M88Officer,M5Manager,M5Officer", authorize.Roles);
    }

    [Fact]
    public async Task List_FiltersByRoleTypeAndAssignedProvince()
    {
        await using var context = CreateContext();
        var records = await AddFacilityDataAsync(context);
        var m5Controller = CreateController(context, Principal("M5Officer", "Province", records.Province.Id));
        var m88Controller = CreateController(context, Principal("M88Officer", "Province", records.Province.Id));

        var m5Items = await ListItems(m5Controller);
        var m88Items = await ListItems(m88Controller);
        var m88PrivateItems = await ListItems(m88Controller, "Private");

        Assert.Equal(new[] { FacilityType.Private }, m5Items.Select(item => Enum.Parse<FacilityType>(item.FacilityType)));
        Assert.Equal(
            new[] { FacilityType.Msy, FacilityType.Gov },
            m88Items.Select(item => Enum.Parse<FacilityType>(item.FacilityType)).OrderBy(type => type));
        Assert.Empty(m88PrivateItems);
    }

    [Fact]
    public async Task List_RestrictsAdminResultsToRequestedSectionTypes()
    {
        await using var context = CreateContext();
        await AddFacilityDataAsync(context);
        var controller = CreateController(context, Principal("Admin", "Mastery"));

        var items = await ListItems(controller, facilityTypes: "Msy,Gov");

        Assert.Equal(
            new[] { FacilityType.Msy, FacilityType.Gov },
            items.Select(item => Enum.Parse<FacilityType>(item.FacilityType)).OrderBy(type => type));
    }

    [Fact]
    public async Task Get_EnforcesFacilityTypeAndGeographicScope()
    {
        await using var context = CreateContext();
        var records = await AddFacilityDataAsync(context);
        var m5Controller = CreateController(context, Principal("M5Officer", "Province", records.Province.Id));
        var m88Controller = CreateController(context, Principal("M88Officer", "Province", records.Province.Id));

        Assert.IsType<OkObjectResult>(await m5Controller.Get(records.Private.Id));
        Assert.IsType<ForbidResult>(await m5Controller.Get(records.Msy.Id));
        Assert.IsType<OkObjectResult>(await m88Controller.Get(records.Gov.Id));
        Assert.IsType<ForbidResult>(await m88Controller.Get(records.Private.Id));
        Assert.IsType<ForbidResult>(await m88Controller.Get(records.OtherProvincePrivate.Id));
    }

    [Fact]
    public async Task Create_EnforcesRoleTypeAndAssignedCity()
    {
        await using var context = CreateContext();
        var records = await AddFacilityDataAsync(context);
        var controller = CreateController(context, Principal("M5Officer", "City", cityId: records.City.Id));

        var wrongType = await controller.Create(CreateDto("Gov", records.City.Id));
        var outOfScope = await controller.Create(CreateDto("Private", records.OtherCity.Id));
        var allowedType = await controller.Create(CreateDto("Private", records.City.Id));

        Assert.IsType<ForbidResult>(wrongType);
        Assert.IsType<ForbidResult>(outOfScope);
        Assert.IsType<CreatedAtActionResult>(allowedType);
        Assert.Equal(5, await context.Facilities.CountAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(
        Province Province,
        City City,
        City OtherCity,
        Facility Private,
        Facility Msy,
        Facility Gov,
        Facility OtherProvincePrivate)> AddFacilityDataAsync(ApplicationDbContext context)
    {
        var province = new Province { Id = Guid.NewGuid(), Name = "استان نمونه" };
        var city = new City { Id = Guid.NewGuid(), Name = "شهر نمونه", ProvinceId = province.Id };
        var otherProvince = new Province { Id = Guid.NewGuid(), Name = "استان دیگر" };
        var otherCity = new City { Id = Guid.NewGuid(), Name = "شهر دیگر", ProvinceId = otherProvince.Id };
        var privateFacility = FacilityInCity(FacilityType.Private, city);
        var msyFacility = FacilityInCity(FacilityType.Msy, city);
        var govFacility = FacilityInCity(FacilityType.Gov, city);
        var otherProvincePrivate = FacilityInCity(FacilityType.Private, otherCity);
        context.AddRange(
            province, city, otherProvince, otherCity,
            privateFacility, msyFacility, govFacility, otherProvincePrivate);
        await context.SaveChangesAsync();

        return (province, city, otherCity, privateFacility, msyFacility, govFacility, otherProvincePrivate);
    }

    private static Facility FacilityInCity(FacilityType type, City city) => new()
    {
        Id = Guid.NewGuid(),
        FacilityType = type,
        Name = $"{type} facility",
        CityId = city.Id,
        City = city
    };

    private static FacilitiesController CreateController(ApplicationDbContext context, ClaimsPrincipal principal)
    {
        return new FacilitiesController(context, new ScopeService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private static ClaimsPrincipal Principal(
        string role,
        string scope,
        Guid? provinceId = null,
        Guid? cityId = null)
    {
        var claims = new List<Claim>
        {
            new(AuthClaimTypes.Role, role),
            new(AuthClaimTypes.ScopeLevel, scope)
        };
        if (provinceId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.ScopeProvinceId, provinceId.Value.ToString()));
        if (cityId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.ScopeCityId, cityId.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    private static FacilityCreateDto CreateDto(string type, Guid cityId) =>
        new(type, "Facility", "Address", "Sport", null, null, cityId);

    private static async Task<List<FacilityOutDto>> ListItems(
        FacilitiesController controller,
        string? facilityType = null,
        string? facilityTypes = null)
    {
        var result = Assert.IsType<OkObjectResult>(
            await controller.List(facilityType, provinceId: null, cityId: null, facilityTypes: facilityTypes));
        var items = result.Value!.GetType().GetProperty("items")!.GetValue(result.Value);
        return Assert.IsAssignableFrom<IEnumerable<FacilityOutDto>>(items).ToList();
    }
}
