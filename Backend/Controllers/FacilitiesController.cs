using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Extensions;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/facilities")]
    [Authorize(Roles = "Admin,Manager,M88Manager,M88Officer,M5Manager,M5Officer")]
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

        private const string PrivateFacilityImportRoles = "Admin,Manager,M5Manager,M5Officer";

        private bool HasFacilityDeletePermission() => IsAdminOrManager() || IsM5Manager() || IsM88Manager();
        private bool HasContractDeletePermission() => IsAdminOrManager() || IsM88Manager();
        private bool HasLicenseDeletePermission() => IsAdminOrManager() || IsM5Manager();

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

        private bool IsWithinCurrentScope(Facility facility)
        {
            var scopeLevel = User.ScopeLevel();

            if (scopeLevel == "Mastery") return true;
            if (scopeLevel == "Province")
                return facility.City.ProvinceId == User.ScopeProvinceId();
            if (scopeLevel == "City")
                return facility.CityId == User.ScopeCityId();

            return false;
        }

        private bool HasFacilityPermission()
        {
            var role = User.Role();
            return role is "Admin" or "Manager" or "M88Manager" or "M88Officer" or "M5Manager" or "M5Officer";
        }

        // Check if user can access a specific facility based on role and type
        private bool CanAccessFacility(Facility facility)
        {
            var role = User.Role();

            if (role is "Admin" or "Manager")
                return IsWithinCurrentScope(facility);

            if (role is "M5Manager" or "M5Officer")
                return facility.FacilityType == FacilityType.Private && IsWithinCurrentScope(facility);

            if (role is "M88Manager" or "M88Officer")
                return facility.FacilityType is FacilityType.Msy or FacilityType.Gov &&
                       IsWithinCurrentScope(facility);

            return false;
        }

        // Check if user can write to facility based on type (for create/update)
        private bool CanWriteFacilityType(FacilityType facilityType)
        {
            var role = User.Role();

            if (role == "Admin" || role == "Manager")
            {
                return true; // Full access
            }

            if (role is "M88Manager" or "M88Officer")
                return facilityType is FacilityType.Msy or FacilityType.Gov;

            if (role is "M5Manager" or "M5Officer")
                return facilityType == FacilityType.Private;

            return false;
        }

        private bool CanImportPrivateFacilityAtScope(Guid provinceId, Guid cityId)
        {
            var scopeLevel = User.ScopeLevel();
            if (scopeLevel == "Mastery")
                return true;
            if (scopeLevel == "Province")
                return User.ScopeProvinceId() == provinceId;
            if (scopeLevel == "City")
                return User.ScopeCityId() == cityId;
            return false;
        }

        [HttpGet("import/template")]
        [Authorize(Roles = PrivateFacilityImportRoles)]
        public IActionResult DownloadPrivateFacilityImportTemplate()
        {
            return File(
                PrivateFacilityImportWorkbook.CreateTemplate(),
                PrivateFacilityImportWorkbook.ContentType,
                "private-facilities-import-template.xlsx");
        }

        [HttpPost("import")]
        [Authorize(Roles = PrivateFacilityImportRoles)]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> ImportPrivateFacilities([FromForm] IFormFile? file)
        {
            if (User.Role() is not ("Admin" or "Manager" or "M5Manager" or "M5Officer"))
                return Forbid("شما اجازه وارد کردن اماکن خصوصی را ندارید");
            if (file == null || file.Length == 0)
                return BadRequest(new { detail = "لطفا فایل اکسل را انتخاب کنید" });
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { detail = "فرمت فایل باید Excel (.xlsx) باشد" });

            IReadOnlyList<PrivateFacilityImportRow> rows;
            try
            {
                await using var stream = file.OpenReadStream();
                rows = PrivateFacilityImportWorkbook.Read(stream);
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new { detail = ex.Message });
            }
            catch (Exception ex) when (ex is IOException or ClosedXML.Excel.Exceptions.ClosedXMLException)
            {
                return BadRequest(new { detail = "فایل اکسل معتبر نیست یا قابل خواندن نیست" });
            }

            var provinces = await _db.Provinces.ToListAsync();
            var cities = await _db.Cities.ToListAsync();
            var facilities = await _scope.FilterFacilities(_db.Facilities
                    .Include(facility => facility.Licenses)
                    .Where(facility => facility.FacilityType == FacilityType.Private), User)
                .ToListAsync();

            var createdFacilities = 0;
            var existingFacilities = 0;
            var createdLicenses = 0;
            var skippedExistingLicenses = 0;
            var skippedInvalidFacilities = 0;
            var skippedInvalidLicenses = 0;
            var skippedOutOfScope = 0;
            var errors = new List<PrivateFacilityImportRowError>();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Name) ||
                    string.IsNullOrWhiteSpace(row.Address) ||
                    string.IsNullOrWhiteSpace(row.ProvinceName) ||
                    string.IsNullOrWhiteSpace(row.CityName))
                {
                    skippedInvalidFacilities++;
                    errors.Add(new PrivateFacilityImportRowError(
                        row.RowNumber, "استان، شهر، نام مکان و آدرس الزامی هستند."));
                    continue;
                }

                var normalizedProvinceName = PrivateFacilityImportWorkbook.NormalizeName(row.ProvinceName);
                var matchingProvinces = provinces.Where(province =>
                    PrivateFacilityImportWorkbook.NormalizeName(province.Name) == normalizedProvinceName).ToList();
                if (matchingProvinces.Count != 1)
                {
                    skippedInvalidFacilities++;
                    errors.Add(new PrivateFacilityImportRowError(row.RowNumber, "نام استان معتبر یا یکتا نیست."));
                    continue;
                }

                var province = matchingProvinces[0];
                var normalizedCityName = PrivateFacilityImportWorkbook.NormalizeName(row.CityName);
                var matchingCities = cities.Where(city =>
                    city.ProvinceId == province.Id &&
                    PrivateFacilityImportWorkbook.NormalizeName(city.Name) == normalizedCityName).ToList();
                if (matchingCities.Count != 1)
                {
                    skippedInvalidFacilities++;
                    errors.Add(new PrivateFacilityImportRowError(
                        row.RowNumber, "نام شهر معتبر نیست یا با استان مطابقت ندارد."));
                    continue;
                }

                var city = matchingCities[0];
                if (!CanImportPrivateFacilityAtScope(province.Id, city.Id))
                {
                    skippedOutOfScope++;
                    continue;
                }

                var normalizedFacilityName = PrivateFacilityImportWorkbook.NormalizeName(row.Name);
                var facility = facilities.FirstOrDefault(existing =>
                    existing.CityId == city.Id &&
                    PrivateFacilityImportWorkbook.NormalizeName(existing.Name) == normalizedFacilityName);
                var facilityAlreadyExists = facility != null;

                if (facility == null)
                {
                    facility = new Facility
                    {
                        FacilityType = FacilityType.Private,
                        Name = row.Name.Trim(),
                        Address = row.Address.Trim(),
                        SportType = row.SportType?.Trim() ?? string.Empty,
                        OwnerName = row.OwnerName?.Trim(),
                        OwnerContact = row.OwnerContact?.Trim(),
                        CityId = city.Id
                    };
                    _db.Facilities.Add(facility);
                    facilities.Add(facility);
                    createdFacilities++;
                }
                else
                {
                    existingFacilities++;
                }

                var hasLicenseData = row.LicenseNumber != null ||
                                     row.LicenseHolderName != null ||
                                     row.LicenseIssueDate != null ||
                                     row.LicenseExpiryDate != null;
                if (!hasLicenseData)
                {
                    if (!facilityAlreadyExists)
                        await _db.SaveChangesAsync();
                    continue;
                }

                var issueDate = default(DateOnly);
                var licenseIsValid =
                    !string.IsNullOrWhiteSpace(row.LicenseNumber) &&
                    !string.IsNullOrWhiteSpace(row.LicenseHolderName) &&
                    PrivateFacilityImportWorkbook.TryParseSolarDate(row.LicenseIssueDate, out issueDate);
                DateOnly? expiryDate = null;
                if (licenseIsValid && row.LicenseExpiryDate != null)
                {
                    if (!PrivateFacilityImportWorkbook.TryParseSolarDate(row.LicenseExpiryDate, out var parsedExpiryDate) ||
                        parsedExpiryDate < issueDate)
                    {
                        licenseIsValid = false;
                    }
                    else
                    {
                        expiryDate = parsedExpiryDate;
                    }
                }

                if (!licenseIsValid)
                {
                    skippedInvalidLicenses++;
                    errors.Add(new PrivateFacilityImportRowError(
                        row.RowNumber, "اطلاعات مجوز معتبر نیست؛ مکان ثبت شد اما مجوز ثبت نشد."));
                    if (!facilityAlreadyExists)
                        await _db.SaveChangesAsync();
                    continue;
                }

                var normalizedLicenseNumber = PrivateFacilityImportWorkbook.NormalizeName(row.LicenseNumber!);
                if (facility.Licenses.Any(license =>
                        PrivateFacilityImportWorkbook.NormalizeName(license.LicenseNumber) == normalizedLicenseNumber))
                {
                    skippedExistingLicenses++;
                    if (!facilityAlreadyExists)
                        await _db.SaveChangesAsync();
                    continue;
                }

                facility.Licenses.Add(new FacilityLicense
                {
                    LicenseNumber = row.LicenseNumber!.Trim(),
                    HolderName = row.LicenseHolderName!.Trim(),
                    IssueDate = issueDate,
                    ExpiryDate = expiryDate
                });
                await _db.SaveChangesAsync();
                createdLicenses++;
            }

            return Ok(new PrivateFacilityImportResultDto(
                createdFacilities,
                existingFacilities,
                createdLicenses,
                skippedExistingLicenses,
                skippedInvalidFacilities,
                skippedInvalidLicenses,
                skippedOutOfScope,
                errors));
        }

        // Helper to check if user can access contracts for a facility
        private bool CanAccessFacilityContractsLicenses(Facility facility)
        {
            var role = User.Role();

            if (role is "Admin" or "Manager")
                return CanAccessFacility(facility);

            if (role == "M88Manager" || role == "M88Officer")
                return CanAccessFacility(facility);

            return false;
        }

        // Helper to check if user can access licenses for a facility
        private bool CanAccessFacilityLicenses(Facility facility)
        {
            var role = User.Role();

            if (role is "Admin" or "Manager")
                return CanAccessFacility(facility);

            if (role == "M5Manager" || role == "M5Officer")
                return (facility.FacilityType is FacilityType.Private or FacilityType.Gov) &&
                       CanAccessFacility(facility);

            return false;
        }

        [HttpGet]
        public async Task<IActionResult> List(
            string? facilityType,
            Guid? provinceId,
            Guid? cityId,
            int page = 1,
            int pageSize = 20,
            string? facilityTypes = null)
        {
            var qs = _db.Facilities
                .Include(f => f.City)
                    .ThenInclude(c => c.Province)
                .AsQueryable();

            // Apply role and scope filtering
            var role = User.Role();

            if (role is "M5Manager" or "M5Officer")
                qs = qs.Where(f => f.FacilityType == FacilityType.Private);
            else if (role is "M88Manager" or "M88Officer")
                qs = qs.Where(f => f.FacilityType == FacilityType.Msy || f.FacilityType == FacilityType.Gov);

            // Apply scope filtering
            qs = _scope.FilterFacilities(qs, User);

            if (!string.IsNullOrEmpty(facilityType))
            {
                if (!Enum.TryParse<FacilityType>(facilityType, out var facilityTypeEnum))
                    return BadRequest(new { detail = "نوع مکان نامعتبر است" });

                qs = qs.Where(f => f.FacilityType == facilityTypeEnum);
            }

            if (!string.IsNullOrWhiteSpace(facilityTypes))
            {
                var requestedTypes = new List<FacilityType>();
                foreach (var type in facilityTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!Enum.TryParse<FacilityType>(type, out var parsedType))
                        return BadRequest(new { detail = "نوع مکان نامعتبر است" });

                    requestedTypes.Add(parsedType);
                }

                qs = qs.Where(f => requestedTypes.Contains(f.FacilityType));
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

            if (!CanWriteFacilityType(facilityTypeEnum))
                return Forbid("شما اجازه ثبت این نوع مکان را ندارید");

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
            var facility = await _db.Facilities
                .Include(f => f.City)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (facility == null) return NotFound();

            if (!CanAccessFacility(facility))
                return Forbid("شما اجازه ویرایش این مکان را ندارید");

            if (!Enum.TryParse<FacilityType>(dto.FacilityType, out var facilityTypeEnum))
                return BadRequest(new { detail = "نوع مکان نامعتبر است" });

            if (!CanWriteFacilityType(facilityTypeEnum))
                return Forbid("شما اجازه ویرایش این نوع مکان را ندارید");

            try { await _scope.CheckCityAllowedAsync(User, dto.CityId); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }

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
            var facility = await _db.Facilities
                .Include(f => f.City)
                .FirstOrDefaultAsync(f => f.Id == id);
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

            if (!CanAccessFacilityContractsLicenses(facility))
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

            if (!CanAccessFacilityContractsLicenses(facility))
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
        public async Task<IActionResult> CreateContract(Guid facilityId, FacilityContractDto dto)
        {
            var facility = await _db.Facilities
                .Include(f => f.City)
                .FirstOrDefaultAsync(f => f.Id == facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacilityContractsLicenses(facility))
                return Forbid("شما اجازه ایجاد قرارداد برای این مکان را ندارید");

            // M88 officers can only create for MSY facilities
            if (User.Role() == "M88Officer" && facility.FacilityType != FacilityType.Msy)
                return Forbid("شما فقط مجاز به ایجاد قرارداد برای اماکن MSY هستید");

            if (!dto.StartDate.HasValue)
                return BadRequest(new { detail = "تاریخ شروع قرارداد الزامی است" });

            var contract = new FacilityContract
            {
                FacilityId = facilityId,
                ThirdPartyName = dto.ThirdPartyName ?? string.Empty,
                ThirdPartyContact = dto.ThirdPartyContact,
                StartDate = DateOnly.FromDateTime(dto.StartDate.Value),
                EndDate = dto.EndDate.HasValue ? DateOnly.FromDateTime(dto.EndDate.Value) : null,
                ContractFilePath = dto.ContractFilePath,
                CreatedAt = DateTime.UtcNow
            };

            _db.FacilityContracts.Add(contract);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetContract), new { facilityId = facilityId, contractId = contract.Id }, new { id = contract.Id });
        }

        [HttpPut("{facilityId}/contracts/{contractId}")]
        public async Task<IActionResult> UpdateContract(Guid facilityId, Guid contractId, FacilityContractDto dto)
        {
            var facility = await _db.Facilities
                .Include(f => f.City)
                .FirstOrDefaultAsync(f => f.Id == facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacilityContractsLicenses(facility))
                return Forbid("شما اجازه ویرایش این قرارداد را ندارید");

            var contract = await _db.FacilityContracts
                .FirstOrDefaultAsync(c => c.FacilityId == facilityId && c.Id == contractId);

            if (contract == null) return NotFound();

            // M88 officers can only update for MSY facilities
            if (User.Role() == "M88Officer" && facility.FacilityType != FacilityType.Msy)
                return Forbid("شما فقط مجاز به ویرایش قرارداد برای اماکن MSY هستید");

            if (!dto.StartDate.HasValue)
                return BadRequest(new { detail = "تاریخ شروع قرارداد الزامی است" });

            contract.ThirdPartyName = dto.ThirdPartyName ?? string.Empty;
            contract.ThirdPartyContact = dto.ThirdPartyContact;
            contract.StartDate = DateOnly.FromDateTime(dto.StartDate.Value);
            contract.EndDate = dto.EndDate.HasValue ? DateOnly.FromDateTime(dto.EndDate.Value) : null;
            contract.ContractFilePath = dto.ContractFilePath;

            await _db.SaveChangesAsync();
            return Ok(new { id = contract.Id });
        }

        [HttpDelete("{facilityId}/contracts/{contractId}")]
        public async Task<IActionResult> DeleteContract(Guid facilityId, Guid contractId)
        {
            var facility = await _db.Facilities
                .Include(f => f.City)
                .FirstOrDefaultAsync(f => f.Id == facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacilityContractsLicenses(facility))
                return Forbid("شما اجازه حذف این قرارداد را ندارید");

            if (!HasContractDeletePermission())
                return Forbid("شما اجازه حذف قرارداد ندارید");

            var contract = await _db.FacilityContracts
                .FirstOrDefaultAsync(c => c.FacilityId == facilityId && c.Id == contractId);

            if (contract == null) return NotFound();

            // M88 officers cannot delete
            if (User.Role() == "M88Officer")
                return Forbid("شما اجازه حذف قرارداد ندارید");

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

            if (!CanAccessFacilityLicenses(facility))
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

            if (!CanAccessFacilityLicenses(facility))
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
        public async Task<IActionResult> CreateLicense(Guid facilityId, FacilityLicenseDto dto)
        {
            var facility = await _db.Facilities
                .Include(f => f.City)
                .FirstOrDefaultAsync(f => f.Id == facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacilityLicenses(facility))
                return Forbid("شما اجازه ایجاد لایسنس برای این مکان را ندارید");

            // M5 officers can only create for Private/Gov facilities
            if (User.Role() == "M5Officer" && facility.FacilityType is not (FacilityType.Private or FacilityType.Gov))
                return Forbid("شما فقط مجاز به ایجاد لایسنس برای اماکن خصوصی/دولتی هستید");

            if (!dto.IssueDate.HasValue)
                return BadRequest(new { detail = "تاریخ صدور لایسنس الزامی است" });

            var license = new FacilityLicense
            {
                FacilityId = facilityId,
                LicenseNumber = dto.LicenseNumber ?? string.Empty,
                HolderName = dto.HolderName ?? string.Empty,
                IssueDate = DateOnly.FromDateTime(dto.IssueDate.Value),
                ExpiryDate = dto.ExpiryDate.HasValue ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : null,
                LicenseFilePath = dto.LicenseFilePath,
                CreatedAt = DateTime.UtcNow
            };

            _db.FacilityLicenses.Add(license);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLicense), new { facilityId = facilityId, licenseId = license.Id }, new { id = license.Id });
        }

        [HttpPut("{facilityId}/licenses/{licenseId}")]
        public async Task<IActionResult> UpdateLicense(Guid facilityId, Guid licenseId, FacilityLicenseDto dto)
        {
            var facility = await _db.Facilities
                .Include(f => f.City)
                .FirstOrDefaultAsync(f => f.Id == facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacilityLicenses(facility))
                return Forbid("شما اجازه ویرایش این لایسنس را ندارید");

            var license = await _db.FacilityLicenses
                .FirstOrDefaultAsync(l => l.FacilityId == facilityId && l.Id == licenseId);

            if (license == null) return NotFound();

            // M5 officers can only update for Private/Gov facilities
            if (User.Role() == "M5Officer" && facility.FacilityType is not (FacilityType.Private or FacilityType.Gov))
                return Forbid("شما فقط مجاز به ویرایش لایسنس برای اماکن خصوصی/دولتی هستید");

            if (!dto.IssueDate.HasValue)
                return BadRequest(new { detail = "تاریخ صدور لایسنس الزامی است" });

            license.LicenseNumber = dto.LicenseNumber ?? string.Empty;
            license.HolderName = dto.HolderName ?? string.Empty;
            license.IssueDate = DateOnly.FromDateTime(dto.IssueDate.Value);
            license.ExpiryDate = dto.ExpiryDate.HasValue ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : null;
            license.LicenseFilePath = dto.LicenseFilePath;

            await _db.SaveChangesAsync();
            return Ok(new { id = license.Id });
        }

        [HttpDelete("{facilityId}/licenses/{licenseId}")]
        public async Task<IActionResult> DeleteLicense(Guid facilityId, Guid licenseId)
        {
            var facility = await _db.Facilities
                .Include(f => f.City)
                .FirstOrDefaultAsync(f => f.Id == facilityId);
            if (facility == null) return NotFound();

            if (!CanAccessFacilityLicenses(facility))
                return Forbid("شما اجازه حذف این لایسنس را ندارید");

            if (!HasLicenseDeletePermission())
                return Forbid("شما اجازه حذف لایسنس ندارید");

            var license = await _db.FacilityLicenses
                .FirstOrDefaultAsync(l => l.FacilityId == facilityId && l.Id == licenseId);

            if (license == null) return NotFound();

            // M5 officers cannot delete
            if (User.Role() == "M5Officer")
                return Forbid("شما اجازه حذف لایسنس ندارید");

            _db.FacilityLicenses.Remove(license);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
