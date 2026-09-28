using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using Backend.Services;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/facilities")]
    [Authorize]
    public class FacilitiesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ScopeService _scope;

        public FacilitiesController(ApplicationDbContext db, ScopeService scope)
        {
            _db = db;
            _scope = scope;
        }

        // Helper methods for permission checks
        private bool IsAdminOrManager() => User.Role() is "Admin" or "Manager";
        private bool IsM88Manager() => User.Role() == "M88Manager";
        private bool IsM88Officer() => User.Role() == "M88Officer";
        private bool IsM5Manager() => User.Role() == "M5Manager";
        private bool IsM5Officer() => User.Role() == "M5Officer";

        private bool HasFacilityDeletePermission() => IsAdminOrManager() || IsM5Manager() || IsM88Manager();
        private bool HasContractDeletePermission() => IsM88Manager();
        private bool HasLicenseDeletePermission() => IsM5Manager();

        private bool CanManageScope(string targetScopeLevel)
        {
            if (!HasFacilityPermission()) return false;

            var myScope = User.ScopeLevel();

            if (myScope == "Mastery") return true;
            if (myScope == "Province")
            {
                return targetScopeLevel == "Province" || targetScopeLevel == "City";
            }
            if (myScope == "City")
            {
                return targetScopeLevel == "City";
            }

            return false;
        }

        private bool HasFacilityPermission()
        {
            var role = User.Role();
            return role is "Admin" or "Manager" or "M88Manager" or "M88Officer" or "M5Manager" or "M5Officer";
        }

        // Check if user's role allows managing this facility type
        private bool CanManageFacilityType(string facilityType, bool requireDelete = false)
        {
            var role = User.Role();

            if (role == "Admin" || role == "Manager")
            {
                return true; // Admin/Manager can manage all types
            }

            if (role == "M88Manager" || role == "M88Officer")
            {
                return facilityType == "Msy";
            }

            if (role == "M5Manager" || role == "M5Officer")
            {
                // M5 users can only create/update/delete Private and Gov facilities
                return facilityType is "Private" or "Gov";
            }

            return false;
        }

        // Check if user can access a specific facility based on role and type
        private bool CanAccessFacility(Facility facility)
        {
            var role = User.Role();
            var myScopeLevel = User.ScopeLevel();

            if (role == "Admin" || role == "Manager")
            {
                // Full access based on scope
                if (myScopeLevel == "Mastery") return true;
                if (myScopeLevel == "Province")
                {
                    var myProvinceId = User.ScopeProvinceId();
                    return facility.City.ProvinceId == myProvinceId;
                }
                if (myScopeLevel == "City")
                {
                    var myCityId = User.ScopeCityId();
                    return facility.CityId == myCityId;
                }
            }

            if (role == "M88Manager" || role == "M88Officer")
            {
                // Only MSY facilities, based on scope
                if (facility.FacilityType != FacilityType.Msy) return false;

                if (myScopeLevel == "Mastery") return true;
                if (myScopeLevel == "Province")
                {
                    var myProvinceId = User.ScopeProvinceId();
                    return facility.City.ProvinceId == myProvinceId;
                }
                if (myScopeLevel == "City")
                {
                    var myCityId = User.ScopeCityId();
                    return facility.CityId == myCityId;
                }
            }

            if (role == "M5Manager" || role == "M5Officer")
            {
                // All types for list/get, but based on scope
                if (myScopeLevel == "Mastery") return true;
                if (myScopeLevel == "Province")
                {
                    var myProvinceId = User.ScopeProvinceId();
                    return facility.City.ProvinceId == myProvinceId;
                }
                if (myScopeLevel == "City")
                {
                    var myCityId = User.ScopeCityId();
                    return facility.CityId == myCityId;
                }
            }

            return false;
        }

        // Check if user can perform write operation on facility based on type
        private bool CanWriteFacilityType(Facility facility)
        {
            var role = User.Role();

            if (role == "Admin" || role == "Manager")
            {
                return true; // Full access
            }

            if (role == "M88Manager" || role == "M88Officer")
            {
                return facility.FacilityType == FacilityType.Msy;
            }

            if (role == "M5Manager" || role == "M5Officer")
            {
                // Can only write Private and Gov facilities
                return facility.FacilityType is FacilityType.Private or FacilityType.Gov;
            }

            return false;
        }

        [HttpGet]
        public async Task<IActionResult> List(string? facilityType, Guid? provinceId, Guid? cityId, int page = 1, int pageSize = 20)
        {
            var qs = _db.Facilities
                .Include(f => f.City)
                    .ThenInclude(c => c.Province)
                .AsQueryable();

            // Apply role and scope filtering
            var role = User.Role();

            if (role == "M88Manager" || role == "M88Officer")
            {
                // M88 users can only see MSY facilities
                qs = qs.Where(f => f.FacilityType == FacilityType.Msy);
            }

            // Apply scope filtering
            qs = _scope.FilterFacilities(qs, User);

            if (!string.IsNullOrEmpty(facilityType))
            {
                if (!Enum.TryParse<FacilityType>(facilityType, out var facilityTypeEnum))
                    return BadRequest(new { detail = "نوع مکان نامعتبر است" });

                qs = qs.Where(f => f.FacilityType == facilityTypeEnum);
            }

            if (cityId.HasValue) qs = qs.Where(f => f.CityId == cityId.Value);
            else if (provinceId.HasValue) qs = qs.Where(f => f.City.ProvinceId == provinceId.Value);

            var count = await qs.CountAsync();
            var items = await qs.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(f => new FacilityOutDto(
                    f.Id, f.FacilityType.ToString(), f.Name, f.Address, f.SportType,
                    f.OwnerName, f.OwnerContact, f.CityId, f.City.ProvinceId, f.City.Name, f.City.Province.Name, f.CreatedAt))
            .ToListAsync();

            return Ok(new { items, count });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var f = await _db.Facilities
                .Include(x => x.City).ThenInclude(c => c.Province)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (f == null) return NotFound();

            if (!CanAccessFacility(f))
                return Forbid("شما اجازه مشاهده این مکان را ندارید");

            return Ok(new FacilityOutDto(
                f.Id, f.FacilityType.ToString(), f.Name, f.Address, f.SportType,
                f.OwnerName, f.OwnerContact, f.CityId, f.City.ProvinceId, f.City.Name, f.City.Province.Name, f.CreatedAt));
        }

        [HttpPost]
        public async Task<IActionResult> Create(FacilityCreateDto dto)
        {
            try { await _scope.CheckCityAllowedAsync(User, dto.CityId); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (KeyNotFoundException) { return BadRequest(new { detail = "شهر یافت نشد" }); }

            var role = User.Role();
            if (!Enum.TryParse<FacilityType>(dto.FacilityType, out var facilityTypeEnum))
                return BadRequest(new { detail = "نوع مکان نامعتبر است" });

            // Check role-based facility type permissions
            if (role == "M88Officer" || role == "M88Manager")
            {
                if (facilityTypeEnum != FacilityType.Msy)
                    return Forbid("شما فقط مجاز به ثبت اماکن MSY هستید");
            }

            if (role == "M5Officer" || role == "M5Manager")
            {
                if (facilityTypeEnum is not (FacilityType.Private or FacilityType.Gov))
                    return Forbid("شما فقط مجاز به ثبت اماکن خصوصی/دولتی هستید");
            }

            // Admin and Manager can create any type (scope already checked by CheckCityAllowedAsync)

            var facility = new Facility
            {
                FacilityType = facilityTypeEnum,
                Name = dto.Name,
                Address = dto.Address,
                SportType = dto.SportType,
                OwnerName = dto.OwnerName,
                OwnerContact = dto.OwnerContact,
                CityId = dto.CityId
            };
            _db.Facilities.Add(facility);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = facility.Id }, new { id = facility.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, FacilityUpdateDto dto)
        {
            var facility = await _db.Facilities.FindAsync(id);
            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه ویرایش این مکان را ندارید");

            if (!CanWriteFacilityType(facility))
                return Forbid("شما اجازه ویرایش این نوع مکان را ندارید");

            try { await _scope.CheckCityAllowedAsync(User, dto.CityId); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }

            if (!Enum.TryParse<FacilityType>(dto.FacilityType, out var facilityTypeEnum))
                return BadRequest(new { detail = "نوع مکان نامعتبر است" });

            facility.FacilityType = facilityTypeEnum;
            facility.Name = dto.Name;
            facility.Address = dto.Address;
            facility.SportType = dto.SportType;
            facility.OwnerName = dto.OwnerName;
            facility.OwnerContact = dto.OwnerContact;
            facility.CityId = dto.CityId;
            facility.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { id = facility.Id });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var facility = await _db.Facilities.FindAsync(id);
            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه حذف این مکان را ندارید");

            if (!HasFacilityDeletePermission())
                return Forbid("شما اجازه حذف مکان را ندارید");

            _db.Facilities.Remove(facility);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // Contract endpoints
        [HttpGet("{id}/contracts")]
        public async Task<IActionResult> GetContracts(Guid id)
        {
            var facility = await _db.Facilities
                .Include(f => f.City).ThenInclude(c => c.Province)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه مشاهده قراردادهای این مکان را ندارید");

            var contracts = await _db.FacilityContracts
                .Where(c => c.FacilityId == id)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.ThirdPartyName,
                    c.ThirdPartyContact,
                    c.StartDate,
                    c.EndDate,
                    c.ContractFilePath,
                    c.CreatedAt
                })
                .ToListAsync();

            return Ok(contracts);
        }

        [HttpGet("{facilityId}/contracts/{contractId}")]
        public async Task<IActionResult> GetContract(Guid facilityId, Guid contractId)
        {
            var facility = await _db.Facilities
                .Include(f => f.City).ThenInclude(c => c.Province)
                .FirstOrDefaultAsync(f => f.Id == facilityId);

            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه مشاهده این قرارداد را ندارید");

            var contract = await _db.FacilityContracts
                .FirstOrDefaultAsync(c => c.FacilityId == facilityId && c.Id == contractId);

            if (contract == null) return NotFound();

            return Ok(new
            {
                contract.Id,
                contract.ThirdPartyName,
                contract.ThirdPartyContact,
                contract.StartDate,
                contract.EndDate,
                contract.ContractFilePath,
                contract.CreatedAt
            });
        }

        [HttpPost("{facilityId}/contracts")]
        public async Task<IActionResult> CreateContract(Guid facilityId, [FromBody] dynamic dto)
        {
            var facility = await _db.Facilities.FindAsync(facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه ایجاد قرارداد برای این مکان را ندارید");

            // Only M88Manager and M88Officer can create contracts
            var role = User.Role();
            if (role != "M88Manager" && role != "M88Officer")
                return Forbid("شما اجازه ایجاد قرارداد ندارید");

            // M88 officers can only create for MSY facilities
            if (role == "M88Officer" && facility.FacilityType != FacilityType.Msy)
                return Forbid("شما فقط مجاز به ایجاد قرارداد برای اماکن MSY هستید");

            var contract = new FacilityContract
            {
                FacilityId = facilityId,
                ThirdPartyName = dto.ThirdPartyName ?? string.Empty,
                ThirdPartyContact = dto.ThirdPartyContact,
                StartDate = DateOnly.FromDateTime(dto.StartDate),
                EndDate = dto.EndDate != null ? DateOnly.FromDateTime(dto.EndDate) : null,
                ContractFilePath = dto.ContractFilePath,
                CreatedAt = DateTime.UtcNow
            };

            _db.FacilityContracts.Add(contract);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetContract), new { facilityId = facilityId, contractId = contract.Id }, new { id = contract.Id });
        }

        [HttpPut("{facilityId}/contracts/{contractId}")]
        public async Task<IActionResult> UpdateContract(Guid facilityId, Guid contractId, [FromBody] dynamic dto)
        {
            var facility = await _db.Facilities.FindAsync(facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه ویرایش این قرارداد را ندارید");

            var contract = await _db.FacilityContracts
                .FirstOrDefaultAsync(c => c.FacilityId == facilityId && c.Id == contractId);

            if (contract == null) return NotFound();

            // Only M88Manager and M88Officer can update contracts
            var role = User.Role();
            if (role != "M88Manager" && role != "M88Officer")
                return Forbid("شما اجازه ویرایش قرارداد ندارید");

            // M88 officers can only update for MSY facilities
            if (role == "M88Officer" && facility.FacilityType != FacilityType.Msy)
                return Forbid("شما فقط مجاز به ویرایش قرارداد برای اماکن MSY هستید");

            contract.ThirdPartyName = dto.ThirdPartyName ?? string.Empty;
            contract.ThirdPartyContact = dto.ThirdPartyContact;
            contract.StartDate = DateOnly.FromDateTime(dto.StartDate);
            contract.EndDate = dto.EndDate != null ? DateOnly.FromDateTime(dto.EndDate) : null;
            contract.ContractFilePath = dto.ContractFilePath;

            await _db.SaveChangesAsync();
            return Ok(new { id = contract.Id });
        }

        [HttpDelete("{facilityId}/contracts/{contractId}")]
        public async Task<IActionResult> DeleteContract(Guid facilityId, Guid contractId)
        {
            var facility = await _db.Facilities.FindAsync(facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه حذف این قرارداد را ندارید");

            if (!HasContractDeletePermission())
                return Forbid("شما اجازه حذف قرارداد ندارید");

            var contract = await _db.FacilityContracts
                .FirstOrDefaultAsync(c => c.FacilityId == facilityId && c.Id == contractId);

            if (contract == null) return NotFound();

            _db.FacilityContracts.Remove(contract);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // License endpoints
        [HttpGet("{id}/licenses")]
        public async Task<IActionResult> GetLicenses(Guid id)
        {
            var facility = await _db.Facilities
                .Include(f => f.City).ThenInclude(c => c.Province)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه مشاهده لایسنس‌های این مکان را ندارید");

            var licenses = await _db.FacilityLicenses
                .Where(l => l.FacilityId == id)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new
                {
                    l.Id,
                    l.LicenseNumber,
                    l.HolderName,
                    l.IssueDate,
                    l.ExpiryDate,
                    l.LicenseFilePath,
                    l.CreatedAt
                })
                .ToListAsync();

            return Ok(licenses);
        }

        [HttpGet("{facilityId}/licenses/{licenseId}")]
        public async Task<IActionResult> GetLicense(Guid facilityId, Guid licenseId)
        {
            var facility = await _db.Facilities
                .Include(f => f.City).ThenInclude(c => c.Province)
                .FirstOrDefaultAsync(f => f.Id == facilityId);

            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه مشاهده این لایسنس را ندارید");

            var license = await _db.FacilityLicenses
                .FirstOrDefaultAsync(l => l.FacilityId == facilityId && l.Id == licenseId);

            if (license == null) return NotFound();

            return Ok(new
            {
                license.Id,
                license.LicenseNumber,
                license.HolderName,
                license.IssueDate,
                license.ExpiryDate,
                license.LicenseFilePath,
                license.CreatedAt
            });
        }

        [HttpPost("{facilityId}/licenses")]
        public async Task<IActionResult> CreateLicense(Guid facilityId, [FromBody] dynamic dto)
        {
            var facility = await _db.Facilities.FindAsync(facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه ایجاد لایسنس برای این مکان را ندارید");

            // Only M5Manager and M5Officer can create licenses
            var role = User.Role();
            if (role != "M5Manager" && role != "M5Officer")
                return Forbid("شما اجازه ایجاد لایسنس ندارید");

            // M5 officers can only create for Private/Gov facilities
            if (role == "M5Officer" && facility.FacilityType is not (FacilityType.Private or FacilityType.Gov))
                return Forbid("شما فقط مجاز به ایجاد لایسنس برای اماکن خصوصی/دولتی هستید");

            var license = new FacilityLicense
            {
                FacilityId = facilityId,
                LicenseNumber = dto.LicenseNumber ?? string.Empty,
                HolderName = dto.HolderName ?? string.Empty,
                IssueDate = DateOnly.FromDateTime(dto.IssueDate),
                ExpiryDate = dto.ExpiryDate != null ? DateOnly.FromDateTime(dto.ExpiryDate) : null,
                LicenseFilePath = dto.LicenseFilePath,
                CreatedAt = DateTime.UtcNow
            };

            _db.FacilityLicenses.Add(license);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLicense), new { facilityId = facilityId, licenseId = license.Id }, new { id = license.Id });
        }

        [HttpPut("{facilityId}/licenses/{licenseId}")]
        public async Task<IActionResult> UpdateLicense(Guid facilityId, Guid licenseId, [FromBody] dynamic dto)
        {
            var facility = await _db.Facilities.FindAsync(facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("��ما اجازه ویرایش این لایسنس را ندارید");

            var license = await _db.FacilityLicenses
                .FirstOrDefaultAsync(l => l.FacilityId == facilityId && l.Id == licenseId);

            if (license == null) return NotFound();

            // Only M5Manager and M5Officer can update licenses
            var role = User.Role();
            if (role != "M5Manager" && role != "M5Officer")
                return Forbid("شما اجازه ویرایش لایسنس ندارید");

            // M5 officers can only update for Private/Gov facilities
            if (role == "M5Officer" && facility.FacilityType is not (FacilityType.Private or FacilityType.Gov))
                return Forbid("شما فقط مجاز به ویرایش لایسنس برای اماکن خصوصی/دولتی هستید");

            license.LicenseNumber = dto.LicenseNumber ?? string.Empty;
            license.HolderName = dto.HolderName ?? string.Empty;
            license.IssueDate = DateOnly.FromDateTime(dto.IssueDate);
            license.ExpiryDate = dto.ExpiryDate != null ? DateOnly.FromDateTime(dto.ExpiryDate) : null;
            license.LicenseFilePath = dto.LicenseFilePath;

            await _db.SaveChangesAsync();
            return Ok(new { id = license.Id });
        }

        [HttpDelete("{facilityId}/licenses/{licenseId}")]
        public async Task<IActionResult> DeleteLicense(Guid facilityId, Guid licenseId)
        {
            var facility = await _db.Facilities.FindAsync(facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه حذف این لایسنس را ندارید");

            if (!HasLicenseDeletePermission())
                return Forbid("شما اجازه حذف لایسنس ندارید");

            var license = await _db.FacilityLicenses
                .FirstOrDefaultAsync(l => l.FacilityId == facilityId && l.Id == licenseId);

            if (license == null) return NotFound();

            _db.FacilityLicenses.Remove(license);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
