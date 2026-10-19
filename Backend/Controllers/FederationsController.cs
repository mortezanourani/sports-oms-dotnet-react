using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Extensions;
using Microsoft.AspNetCore.Identity;
using Backend.Services;
using DocumentFormat.OpenXml.Packaging;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/federations")]
    [Authorize] // read access is open to any authenticated user; write actions are checked manually below
    public class FederationsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FederationPositionService _federationPositionService;
        public FederationsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            FederationPositionService federationPositionService)
        {
            _db = db;
            _userManager = userManager;
            _federationPositionService = federationPositionService;
        }

        // Users who can manage federations (Admin, Manager, SportsManager, SportsOfficer)
        private static readonly string[] FederationManagerRoles = { "Admin", "Manager", "SportsManager", "SportsOfficer" };

        // Users who can delete federations (excludes SportsOfficer)
        private static readonly string[] FederationDeleterRoles = { "Admin", "Manager", "SportsManager" };

        private bool HasFederationPermission() => FederationManagerRoles.Contains(User.Role());

        private bool HasDeletePermission() => FederationDeleterRoles.Contains(User.Role());

        private const string FederationImportRoles = "Admin,Manager,SportsManager,SportsOfficer";
        private const string FederationRepImportPassword = "P@ssw0rd";

        [HttpGet("import/template")]
        [Authorize(Roles = FederationImportRoles)]
        public IActionResult DownloadImportTemplate()
        {
            return File(
                FederationImportWorkbook.CreateTemplate(),
                FederationImportWorkbook.ContentType,
                "federations-import-template.xlsx");
        }

        [HttpPost("import")]
        [Authorize(Roles = FederationImportRoles)]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Import([FromForm] IFormFile? file)
        {
            if (!HasFederationPermission())
                return Forbid("شما اجازه وارد کردن فدراسیون را ندارید");
            if (file == null || file.Length == 0)
                return BadRequest(new { detail = "لطفا فایل اکسل را انتخاب کنید" });
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { detail = "فرمت فایل باید Excel (.xlsx) باشد" });

            IReadOnlyList<FederationImportRow> rows;
            try
            {
                await using var stream = file.OpenReadStream();
                rows = FederationImportWorkbook.Read(stream);
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new { detail = ex.Message });
            }
            catch (ArgumentException)
            {
                return BadRequest(new { detail = "فایل اکسل معتبر نیست" });
            }
            catch (IOException)
            {
                return BadRequest(new { detail = "خواندن فایل اکسل با خطا مواجه شد" });
            }
            catch (OpenXmlPackageException)
            {
                return BadRequest(new { detail = "فایل اکسل معتبر نیست" });
            }

            var provinces = await _db.Provinces.ToListAsync();
            var cities = await _db.Cities.ToListAsync();
            var nationalFederations = await _db.Federations
                .Where(item => item.Level == FederationLevel.National)
                .ToListAsync();
            var createdFederations = 0;
            var skippedExistingFederations = 0;
            var createdRepAccounts = 0;
            var assignedPositions = 0;
            var skippedRepAssignments = 0;
            var skippedInvalid = 0;
            var skippedOutOfScope = 0;
            var errors = new List<FederationImportRowError>();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.FederationName))
                {
                    skippedInvalid++;
                    errors.Add(new FederationImportRowError(row.RowNumber, "نام فدراسیون الزامی است."));
                    continue;
                }

                var federationName = row.FederationName.Trim();
                var normalizedFederationName = FederationImportWorkbook.NormalizeName(federationName);
                Province? province = null;
                City? city = null;

                if (row.CityName != null && row.ProvinceName == null)
                {
                    skippedInvalid++;
                    errors.Add(new FederationImportRowError(row.RowNumber, "برای تعیین شهرستان، نام استان نیز الزامی است."));
                    continue;
                }

                if (row.ProvinceName != null)
                {
                    var provinceKey = FederationImportWorkbook.NormalizeName(row.ProvinceName);
                    var provinceMatches = provinces.Where(item =>
                        FederationImportWorkbook.NormalizeName(item.Name) == provinceKey).ToList();
                    if (provinceMatches.Count != 1)
                    {
                        skippedInvalid++;
                        errors.Add(new FederationImportRowError(row.RowNumber, "نام استان معتبر نیست یا تکراری است."));
                        continue;
                    }

                    province = provinceMatches[0];
                }

                if (row.CityName != null)
                {
                    var cityKey = FederationImportWorkbook.NormalizeName(row.CityName);
                    var cityMatches = cities.Where(item =>
                        item.ProvinceId == province!.Id &&
                        FederationImportWorkbook.NormalizeName(item.Name) == cityKey).ToList();
                    if (cityMatches.Count != 1)
                    {
                        skippedInvalid++;
                        errors.Add(new FederationImportRowError(row.RowNumber, "نام شهرستان با استان مطابقت ندارد یا معتبر نیست."));
                        continue;
                    }

                    city = cityMatches[0];
                }

                var level = city != null
                    ? FederationLevel.City
                    : province != null
                        ? FederationLevel.Provincial
                        : FederationLevel.National;
                if (!CanImportFederationAtScope(level, province?.Id, city?.Id))
                {
                    skippedOutOfScope++;
                    continue;
                }

                Federation? federation;
                if (level == FederationLevel.National)
                {
                    federation = nationalFederations.FirstOrDefault(item =>
                        item.Name != null &&
                        FederationImportWorkbook.NormalizeName(item.Name) == normalizedFederationName);
                    if (federation == null)
                    {
                        federation = new Federation
                        {
                            Name = federationName,
                            Level = FederationLevel.National,
                            Category = null
                        };
                        _db.Federations.Add(federation);
                        await _db.SaveChangesAsync();
                        nationalFederations.Add(federation);
                        createdFederations++;
                    }
                    else
                    {
                        skippedExistingFederations++;
                    }
                }
                else
                {
                    var nationalFederation = nationalFederations.FirstOrDefault(item =>
                        item.Name != null &&
                        FederationImportWorkbook.NormalizeName(item.Name) == normalizedFederationName);
                    if (nationalFederation == null)
                    {
                        skippedInvalid++;
                        errors.Add(new FederationImportRowError(
                            row.RowNumber, "فدراسیون کشوری والد از قبل ثبت نشده است."));
                        continue;
                    }

                    Federation? provincialFederation = null;
                    if (level == FederationLevel.City)
                    {
                        provincialFederation = await _db.Federations.FirstOrDefaultAsync(item =>
                            item.Level == FederationLevel.Provincial &&
                            item.ParentFederationId == nationalFederation.Id &&
                            item.ProvinceId == province!.Id);
                        if (provincialFederation == null)
                        {
                            if (!CanManageProvincial(province!.Id))
                            {
                                skippedOutOfScope++;
                                continue;
                            }

                            provincialFederation = new Federation
                            {
                                Level = FederationLevel.Provincial,
                                ParentFederationId = nationalFederation.Id,
                                ProvinceId = province.Id,
                                Category = nationalFederation.Category
                            };
                            _db.Federations.Add(provincialFederation);
                            await _db.SaveChangesAsync();
                            createdFederations++;
                        }
                    }

                    federation = level == FederationLevel.Provincial
                        ? await _db.Federations.FirstOrDefaultAsync(item =>
                            item.Level == FederationLevel.Provincial &&
                            item.ParentFederationId == nationalFederation.Id &&
                            item.ProvinceId == province!.Id)
                        : await _db.Federations.FirstOrDefaultAsync(item =>
                            item.Level == FederationLevel.City &&
                            item.ParentFederationId == provincialFederation!.Id &&
                            item.CityId == city!.Id);

                    if (federation == null)
                    {
                        federation = new Federation
                        {
                            Level = level,
                            ParentFederationId = level == FederationLevel.Provincial
                                ? nationalFederation.Id
                                : provincialFederation!.Id,
                            ProvinceId = level == FederationLevel.Provincial ? province!.Id : null,
                            CityId = level == FederationLevel.City ? city!.Id : null,
                            Category = nationalFederation.Category
                        };
                        _db.Federations.Add(federation);
                        await _db.SaveChangesAsync();
                        createdFederations++;
                    }
                    else
                    {
                        skippedExistingFederations++;
                    }
                }

                if (!CanImportFederationRepAtScope(level, province?.Id, city?.Id))
                {
                    skippedOutOfScope++;
                    continue;
                }

                var repValidationError = ValidateImportedRep(row, out var position, out var startDate,
                    out var endDate, out var gender, out var phoneNumber);
                if (repValidationError != null)
                {
                    skippedRepAssignments++;
                    errors.Add(new FederationImportRowError(row.RowNumber, repValidationError));
                    continue;
                }

                var username = row.Username!.Trim();
                var user = await _userManager.FindByNameAsync(username);
                var isNewUser = user == null;
                user ??= new ApplicationUser
                {
                    UserName = username,
                    Email = $"{username}@local",
                    FirstName = row.FirstName!.Trim(),
                    LastName = row.LastName!.Trim(),
                    PhoneNumber = phoneNumber,
                    Gender = gender,
                    Role = UserRole.FederationRep,
                    ScopeLevel = level switch
                    {
                        FederationLevel.National => ScopeLevel.Mastery,
                        FederationLevel.Provincial => ScopeLevel.Province,
                        FederationLevel.City => ScopeLevel.City,
                        _ => throw new InvalidOperationException("Unsupported federation level.")
                    },
                    ScopeProvinceId = province?.Id,
                    ScopeCityId = city?.Id,
                    EmailConfirmed = true
                };

                await using var transaction = await _db.Database.BeginTransactionAsync();
                if (isNewUser)
                {
                    var createResult = await _userManager.CreateAsync(user, FederationRepImportPassword);
                    if (!createResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _db.Entry(user).State = EntityState.Detached;
                        skippedRepAssignments++;
                        errors.Add(new FederationImportRowError(
                            row.RowNumber,
                            string.Join(" / ", createResult.Errors.Select(error => error.Description))));
                        continue;
                    }
                }

                var (assignedPosition, assignmentError) = await _federationPositionService.AssignAsync(
                    user, federation, position!.Value, TehranDateTime.ToUtc(startDate!.Value),
                    endDate.HasValue ? TehranDateTime.ToUtc(endDate.Value) : null,
                    persistCalculatedEndDateWhenUnspecified: false);
                if (assignmentError != null)
                {
                    await transaction.RollbackAsync();
                    if (isNewUser)
                        _db.Entry(user).State = EntityState.Detached;
                    skippedRepAssignments++;
                    errors.Add(new FederationImportRowError(row.RowNumber, assignmentError));
                    continue;
                }

                await transaction.CommitAsync();
                if (isNewUser)
                    createdRepAccounts++;
                assignedPositions++;
            }

            return Ok(new FederationImportResultDto(
                createdFederations,
                skippedExistingFederations,
                createdRepAccounts,
                assignedPositions,
                skippedRepAssignments,
                skippedInvalid,
                skippedOutOfScope,
                errors));
        }

        private bool CanImportFederationAtScope(
            FederationLevel level,
            Guid? provinceId,
            Guid? cityId) =>
            level switch
            {
                FederationLevel.National => CanManageMastery(),
                FederationLevel.Provincial => provinceId.HasValue && CanManageProvincial(provinceId.Value),
                FederationLevel.City => provinceId.HasValue && cityId.HasValue &&
                                        CanManageCity(cityId.Value, provinceId.Value),
                _ => false
            };

        private bool CanImportFederationRepAtScope(
            FederationLevel level,
            Guid? provinceId,
            Guid? cityId)
        {
            if (!HasFederationPermission())
                return false;

            return level switch
            {
                FederationLevel.National => User.ScopeLevel() == "Mastery",
                FederationLevel.Provincial => provinceId.HasValue &&
                    (User.ScopeLevel() == "Mastery" ||
                     User.ScopeLevel() == "Province" && User.ScopeProvinceId() == provinceId),
                FederationLevel.City => cityId.HasValue && provinceId.HasValue &&
                    (User.ScopeLevel() == "Mastery" ||
                     User.ScopeLevel() == "Province" && User.ScopeProvinceId() == provinceId ||
                     User.ScopeLevel() == "City" && User.ScopeCityId() == cityId),
                _ => false
            };
        }

        private static string? ValidateImportedRep(
            FederationImportRow row,
            out FederationPositionType? position,
            out DateOnly? startDate,
            out DateOnly? endDate,
            out Gender? gender,
            out string? phoneNumber)
        {
            position = null;
            startDate = null;
            endDate = null;
            gender = null;
            phoneNumber = null;

            if (string.IsNullOrWhiteSpace(row.Username) ||
                string.IsNullOrWhiteSpace(row.FirstName) ||
                string.IsNullOrWhiteSpace(row.LastName) ||
                string.IsNullOrWhiteSpace(row.Position) ||
                string.IsNullOrWhiteSpace(row.StartDate))
                return "اطلاعات الزامی نماینده ناقص است؛ فدراسیون ثبت شد اما انتصاب نماینده انجام نشد.";

            if (!FederationImportWorkbook.TryParsePosition(row.Position, out var parsedPosition))
                return "سمت نماینده معتبر نیست؛ فدراسیون ثبت شد اما انتصاب نماینده انجام نشد.";
            if (!FederationImportWorkbook.TryParseSolarDate(row.StartDate, out var parsedStartDate))
                return "تاریخ انتصاب باید تاریخ شمسی معتبر با قالب سال/ماه/روز باشد؛ فدراسیون ثبت شد اما انتصاب نماینده انجام نشد.";

            position = parsedPosition;
            startDate = parsedStartDate;

            if (row.EndDate != null)
            {
                if (!FederationImportWorkbook.TryParseSolarDate(row.EndDate, out var parsedEndDate))
                    return "تاریخ اتمام معتبر نیست؛ فدراسیون ثبت شد اما انتصاب نماینده انجام نشد.";
                endDate = parsedEndDate;
            }

            if (row.Gender != null)
            {
                if (!FederationImportWorkbook.TryParseGender(row.Gender, out var parsedGender))
                    return "جنسیت نماینده معتبر نیست؛ فدراسیون ثبت شد اما انتصاب نماینده انجام نشد.";
                gender = parsedGender;
            }

            if (row.PhoneNumber != null)
            {
                if (!FederationImportWorkbook.TryParsePhoneNumber(row.PhoneNumber, out phoneNumber))
                    return "شماره همراه نماینده معتبر نیست؛ فدراسیون ثبت شد اما انتصاب نماینده انجام نشد.";
            }

            return null;
        }

        // Can this user manage entities at the given scope level or lower?
        // Hierarchy: Mastery > Province > City
        private bool CanManageScope(string targetScopeLevel)
        {
            if (!HasFederationPermission()) return false;

            var myScope = User.ScopeLevel();
            
            // Mastery can manage everything
            if (myScope == "Mastery") return true;
            
            // Province can manage Province and City scopes
            if (myScope == "Province")
            {
                return targetScopeLevel == "Province" || targetScopeLevel == "City";
            }
            
            // City can only manage City scope
            if (myScope == "City")
            {
                return targetScopeLevel == "City";
            }
            
            return false;
        }

        // Can this user manage a National-level federation? Only if their
        // OWN scope is National - a Province- or City-scoped user cannot
        // touch the national federation itself, only their own region's entries.
        private bool CanManageMastery()
        {
            if (!HasFederationPermission()) return false;
            return User.ScopeLevel() == "Mastery";
        }

        // Can this user manage a Provincial federation for a given province?
        // Province-scoped users can only manage their own province.
        // City-scoped users cannot manage province-level federations.
        private bool CanManageProvincial(Guid targetProvinceId)
        {
            if (!HasFederationPermission()) return false;
            
            var myScope = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            
            // Mastery can manage any province
            if (myScope == "Mastery") return true;
            
            // Province can only manage their own province
            if (myScope == "Province") return myProvinceId == targetProvinceId;
            
            // City cannot manage province-level federations
            return false;
        }

        // Can this user manage a City federation for a given city?
        // City-scoped users can only manage their own city.
        // Province-scoped users can manage all cities in their province.
        // Mastery can manage all cities.
        private bool CanManageCity(Guid targetCityId, Guid targetProvinceId)
        {
            if (!HasFederationPermission()) return false;
            
            var myScope = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            var myCityId = User.ScopeCityId();
            
            // Mastery can manage any city
            if (myScope == "Mastery") return true;
            
            // Province can manage all cities in their province
            if (myScope == "Province") return myProvinceId == targetProvinceId;
            
            // City can only manage their own city
            if (myScope == "City") return myCityId == targetCityId;
            
            return false;
        }

        // Filter federations query based on user's scope level for read operations
        private IQueryable<Federation> ApplyScopeFilter(IQueryable<Federation> query)
        {
            var myScopeLevel = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            
            if (myScopeLevel == "Province")
            {
                query = query.Where(f => 
                    (f.Level == FederationLevel.Provincial && f.ProvinceId == myProvinceId) ||
                    (f.Level == FederationLevel.City && f.City!.ProvinceId == myProvinceId));
            }
            else if (myScopeLevel == "City")
            {
                var myCityId = User.ScopeCityId();
                query = query.Where(f => f.Level == FederationLevel.City && f.CityId == myCityId);
            }
            // Mastery users can see everything (no filter needed)
            
            return query;
        }

        private static string ComposeDisplayName(Federation f)
        {
            var federationName = f.Level == FederationLevel.City
                ? f.ParentFederation?.ParentFederation?.Name
                : f.ParentFederation?.Name;

            return f.Level switch
            {
                FederationLevel.National => $"فدراسیون {f.Name}",
                FederationLevel.Provincial => $"هیات {federationName} استان {f.Province?.Name}",
                FederationLevel.City => $"هیات {federationName} شهرستان {f.City?.Name}",
                _ => string.Empty,
            };
        }

        [HttpGet("parent-options")]
        public async Task<IActionResult> ListParentOptions(string level, Guid? provinceId)
        {
            if (!Enum.TryParse<FederationLevel>(level, out var levelEnum) ||
                levelEnum is not (FederationLevel.National or FederationLevel.Provincial))
                return BadRequest(new { detail = "سطح فدراسیون والد نامعتبر است" });

            var myScopeLevel = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            var query = _db.Federations
                .Include(f => f.ParentFederation)
                .Include(f => f.Province)
                .AsQueryable();

            if (levelEnum == FederationLevel.National)
            {
                if (myScopeLevel == "City")
                    return Forbid();

                query = query.Where(f => f.Level == FederationLevel.National);
            }
            else
            {
                if (myScopeLevel is "Province" or "City")
                {
                    if (provinceId.HasValue && provinceId != myProvinceId)
                        return Forbid();
                    provinceId = myProvinceId;
                }

                query = query.Where(f => f.Level == FederationLevel.Provincial);
                if (provinceId.HasValue)
                    query = query.Where(f => f.ProvinceId == provinceId.Value);
            }

            var federations = await query
                .OrderBy(f => f.Name ?? f.Province!.Name)
                .ToListAsync();
            var options = federations
                .Select(f => new FederationParentOptionDto(f.Id, ComposeDisplayName(f)))
                .ToList();

            return Ok(options);
        }

        [HttpGet]
        public async Task<IActionResult> List(string? level, Guid? parentFederationId, Guid? provinceId, Guid? cityId)
        {
            var qs = _db.Federations
                .Include(f => f.ParentFederation).ThenInclude(f => f!.ParentFederation)
                .Include(f => f.Province).Include(f => f.City)
                .Include(f => f.ChildFederations)
                .AsQueryable();

            // Apply scope filter based on user's scope level
            qs = ApplyScopeFilter(qs);

            if (!string.IsNullOrEmpty(level))
            {
                if (!Enum.TryParse<FederationLevel>(level, out var levelEnum))
                    return BadRequest(new { detail = "سطح فurasیون نامعتبر است" });
                qs = qs.Where(f => f.Level == levelEnum);
            }

            if (cityId.HasValue)
                qs = qs.Where(f => f.CityId == cityId.Value);
            else if (provinceId.HasValue)
                qs = qs.Where(f => f.Level == FederationLevel.City
                    ? f.City!.ProvinceId == provinceId.Value
                    : f.ProvinceId == provinceId.Value);
            
            if (parentFederationId.HasValue)
                qs = qs.Where(f => f.ParentFederationId == parentFederationId.Value);
            else if (string.IsNullOrEmpty(level))
                qs = qs.Where(f => f.Level == FederationLevel.National);

            var federations = await qs.OrderBy(f => f.Name ?? f.Province!.Name).ToListAsync();

            var result = federations.Select(f => new FederationOutDto(
                f.Id, ComposeDisplayName(f), f.Category?.ToString(), f.Level.ToString(),
                f.ParentFederationId, f.ParentFederation?.Name,
                f.ProvinceId, f.Province?.Name, f.CityId, f.City?.Name,
                f.ChildFederations.Any()));

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var f = await _db.Federations
                .Include(x => x.ParentFederation).ThenInclude(x => x!.ParentFederation)
                .Include(x => x.Province).Include(x => x.City).Include(x => x.ChildFederations)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (f == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            // Check if user has access to this federation based on scope
            var myScopeLevel = User.ScopeLevel();
            
            if (myScopeLevel == "Province")
            {
                var myProvinceId = User.ScopeProvinceId();
                if (f.Level == FederationLevel.Provincial && f.ProvinceId != myProvinceId)
                    return Forbid("شما اجازه مشاهده این فurasیون را ندارید");
                if (f.Level == FederationLevel.City && f.ProvinceId != myProvinceId)
                    return Forbid("شما اجازه مشاهده این فurasیون را ندارید");
            }
            else if (myScopeLevel == "City")
            {
                var myCityId = User.ScopeCityId();
                if (f.Level != FederationLevel.City || f.CityId != myCityId)
                    return Forbid("شما اجازه مشاهده این فurasیون را ندارید");
            }

            return Ok(new FederationOutDto(
                f.Id, ComposeDisplayName(f), f.Category?.ToString(), f.Level.ToString(),
                f.ParentFederationId, f.ParentFederation?.Name,
                f.ProvinceId, f.Province?.Name, f.CityId, f.City?.Name,
                f.ChildFederations.Any()));
        }

        [HttpPost]
        public async Task<IActionResult> Create(FederationCreateDto dto)
        {
            if (!Enum.TryParse<FederationLevel>(dto.Level, out var levelEnum))
                return BadRequest(new { detail = "سطح فurasیون نامعتبر است" });

            if (levelEnum == FederationLevel.National)
            {
                if (!CanManageMastery())
                    return Forbid("فقط کاربران با سطح دسترسی ملی مجاز به ایجاد فurasیون ملی هستند");

                if (string.IsNullOrWhiteSpace(dto.Name))
                    return BadRequest(new { detail = "لطفا نام فدراسیون را وارد کنید" });
                FederationCategory? category = null;
                if (!string.IsNullOrWhiteSpace(dto.Category))
                {
                    if (!Enum.TryParse<FederationCategory>(dto.Category, out var categoryEnum) ||
                        !Enum.IsDefined(categoryEnum))
                        return BadRequest(new { detail = "دسته‌بندی نامعتبر است" });
                    category = categoryEnum;
                }
                if (dto.ParentFederationId.HasValue || dto.ProvinceId.HasValue || dto.CityId.HasValue)
                    return BadRequest(new { detail = "فurasیون ملی نباید والد یا موقعیت جغرافیایی داشته باشد" });

                var duplicate = await _db.Federations.AnyAsync(f => f.Level == FederationLevel.National && f.Name == dto.Name);
                if (duplicate) return Conflict(new { detail = "این نام برای فurasیون ملی قبلا استفاده شده است" });

                var federation = new Federation { Name = dto.Name, Category = category, Level = FederationLevel.National };
                _db.Federations.Add(federation);
                await _db.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = federation.Id }, new { id = federation.Id });
            }

            if (levelEnum == FederationLevel.Provincial)
            {
                if (!dto.ParentFederationId.HasValue || !dto.ProvinceId.HasValue)
                    return BadRequest(new { detail = "لطفا والد و استان مربوطه را کامل وارد کنید" });
                if (dto.CityId.HasValue)
                    return BadRequest(new { detail = "فurasیون استانی نباید دارای شهر باشد" });

                if (!CanManageProvincial(dto.ProvinceId.Value))
                    return Forbid("شما اجازه ایجاد فurasیون در این استان را ندارید");

                var myProvinceId = User.ScopeProvinceId();
                var parent = await _db.Federations.FindAsync(dto.ParentFederationId.Value);
                if (parent == null || parent.Level != FederationLevel.National)
                    return BadRequest(new { detail = "فurasیون والد معتبر نیست" });

                if (!await _db.Provinces.AnyAsync(p => p.Id == dto.ProvinceId.Value))
                    return BadRequest(new { detail = "استان وارد شده معتبر نیست" });

                // Verify province is within user's scope
                if (User.ScopeLevel() == "Province" && myProvinceId != dto.ProvinceId.Value)
                    return Forbid("شما تنها مجاز به مدیریت فurasیون‌های استانی در استان خود هستید");

                var duplicate = await _db.Federations.AnyAsync(f =>
                    f.ParentFederationId == dto.ParentFederationId && f.ProvinceId == dto.ProvinceId);
                if (duplicate) return Conflict(new { detail = "این فurasیون استانی قبلا برای این استان ثبت شده است" });

                var federation = new Federation
                {
                    Category = parent.Category,
                    Level = FederationLevel.Provincial,
                    ParentFederationId = parent.Id,
                    ProvinceId = dto.ProvinceId,
                };
                _db.Federations.Add(federation);
                await _db.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = federation.Id }, new { id = federation.Id });
            }

            // City
            {
                if (!dto.ParentFederationId.HasValue || !dto.CityId.HasValue)
                    return BadRequest(new { detail = "لطفا والد و شهر مربوطه را کامل وارد کنید" });
                if (dto.ProvinceId.HasValue)
                    return BadRequest(new { detail = "فurasیون شهری نباید دارای استان مستقل باشد" });

                var city = await _db.Cities.FindAsync(dto.CityId.Value);
                if (city == null) return BadRequest(new { detail = "شهر وارد شده معتبر نیست" });

                if (!CanManageCity(dto.CityId.Value, city.ProvinceId))
                    return Forbid("شما اجازه ایجاد فurasیون در این شهر را ندارید");

                var myProvinceId = User.ScopeProvinceId();
                var parent = await _db.Federations.FindAsync(dto.ParentFederationId.Value);
                if (parent == null || parent.Level != FederationLevel.Provincial)
                    return BadRequest(new { detail = "فurasیون والد معتبر نیست" });

                // Verify city is within user's scope
                if (User.ScopeLevel() == "Province" && myProvinceId != city.ProvinceId)
                    return Forbid("شما تنها مجاز به مدیریت فurasیون‌های شهری در شهرهای استان خود هستید");
                if (User.ScopeLevel() == "City" && User.ScopeCityId() != dto.CityId.Value)
                    return Forbid("شما تنها مجاز به مدیریت فurasیون‌های شهری در شهر خود هستید");

                if (city.ProvinceId != parent.ProvinceId)
                    return BadRequest(new { detail = "استان شهر وارد شده با استان فurasیون والد مطابقت ندارد" });

                var duplicate = await _db.Federations.AnyAsync(f =>
                    f.ParentFederationId == dto.ParentFederationId && f.CityId == dto.CityId);
                if (duplicate) return Conflict(new { detail = "این فurasیون شهری قبلا برای این شهر ثبت شده است" });

                var federation = new Federation
                {
                    Category = parent.Category,
                    Level = FederationLevel.City,
                    ParentFederationId = parent.Id,
                    CityId = dto.CityId,
                };
                _db.Federations.Add(federation);
                await _db.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = federation.Id }, new { id = federation.Id });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, FederationUpdateDto dto)
        {
            var federation = await _db.Federations.FindAsync(id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            if (federation.Level != FederationLevel.National)
                return BadRequest(new { detail = "فدراسیون‌های استانی و شهری قابل ویرایش نیستند" });

            if (!CanManageMastery())
                return Forbid("فقط کاربران با سطح دسترسی ملی مجاز به ویرایش فurasیون ملی هستند");

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { detail = "لطفا نام فدراسیون را وارد کنید" });

            FederationCategory? category = null;
            if (!string.IsNullOrWhiteSpace(dto.Category))
            {
                if (!Enum.TryParse<FederationCategory>(dto.Category, out var categoryEnum) ||
                    !Enum.IsDefined(categoryEnum))
                    return BadRequest(new { detail = "دسته‌بندی نامعتبر است" });
                category = categoryEnum;
            }

            federation.Name = dto.Name;
            federation.Category = category;
            var parentIds = new List<Guid> { federation.Id };
            while (parentIds.Count > 0)
            {
                var children = await _db.Federations
                    .Where(child => child.ParentFederationId.HasValue &&
                        parentIds.Contains(child.ParentFederationId.Value))
                    .ToListAsync();
                foreach (var child in children)
                    child.Category = category;
                parentIds = children.Select(child => child.Id).ToList();
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!HasDeletePermission())
                return Forbid("شما اجازه حذف فurasیون‌ها را ندارید");

            var federation = await _db.Federations
                .Include(f => f.Province).Include(f => f.City).ThenInclude(c => c!.Province)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            var allowed = federation.Level switch
            {
                FederationLevel.National => CanManageMastery(),
                FederationLevel.Provincial => CanManageProvincial(federation.ProvinceId!.Value),
                FederationLevel.City => CanManageCity(federation.CityId!.Value, federation.City!.ProvinceId),
                _ => false,
            };
            if (!allowed) return Forbid("شما اجازه حذف این فurasیون را ندارید");

            try
            {
                _db.Federations.Remove(federation);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { detail = "این فurasیون دارای ارتباطاتی است و قابل حذف نیست" });
            }

            return Ok(new { success = true });
        }

        [HttpGet("{id}/positions")]
        public async Task<IActionResult> GetPositions(Guid id)
        {
            var exists = await _db.Federations.AnyAsync(f => f.Id == id);
            if (!exists) return NotFound(new { detail = "فurasیون یافت نشد" });

            var positions = await _db.FederationPositions
                .Include(p => p.User).Include(p => p.Federation)
                .Where(p => p.FederationId == id)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            return Ok(positions.Select(p => new FederationPositionOutDto(
                p.Id, p.Position.ToString(), p.StartDate,
                p.EndDate ?? FederationPositionService.GetTermEndDate(p.StartDate),
                p.UserId, p.User.FirstName ?? "", p.User.LastName ?? "",
                p.FederationId, p.Federation.Name ?? "")));
        }

        // FederationRep membership is represented by federation position history;
        // a user may keep a main role and hold positions across different federations.
        [HttpGet("{id}/federation-reps")]
        public async Task<IActionResult> GetFederationReps(Guid id)
        {
            var federation = await _db.Federations
                .Include(f => f.Province).Include(f => f.City).ThenInclude(c => c!.Province)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            if (!CanManageFederationRepScope(federation))
                return Forbid("شما اجازه مشاهده نمایندگان فurasیون در این سطح را ندارید");

            var today = TehranDateTime.TodayUtc;
            var assignments = await _db.FederationPositions
                .Include(p => p.User)
                .Where(p => p.FederationId == id && p.StartDate <= today)
                .ToListAsync();
            var reps = assignments
                .Where(p => (p.EndDate ?? FederationPositionService.GetTermEndDate(p.StartDate)) > today)
                .GroupBy(p => p.UserId)
                .Select(group => ToFederationRepOutDto(group.OrderByDescending(p => p.StartDate).First().User, federation))
                .OrderBy(user => user.Username)
                .ToList();

            return Ok(reps);
        }

        [HttpGet("{id}/federation-reps/{userId}")]
        public async Task<IActionResult> GetFederationRep(Guid id, Guid userId)
        {
            var federation = await _db.Federations
                .Include(f => f.Province).Include(f => f.City).ThenInclude(c => c!.Province)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            if (!CanManageFederationRepScope(federation))
                return Forbid("شما اجازه مشاهده نمایندگان فurasیون در این سطح را ندارید");

            var today = TehranDateTime.TodayUtc;
            var assignment = await _db.FederationPositions
                .Include(p => p.User)
                .Where(p => p.FederationId == id && p.UserId == userId && p.StartDate <= today)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
            var currentAssignment = assignment.FirstOrDefault(p =>
                (p.EndDate ?? FederationPositionService.GetTermEndDate(p.StartDate)) > today);
            if (currentAssignment == null) return NotFound(new { detail = "نماینده فدراسیون یافت نشد" });

            return Ok(ToFederationRepOutDto(currentAssignment.User, federation));
        }

        [HttpPost("{id}/federation-reps")]
        public async Task<IActionResult> CreateFederationRep(Guid id, FederationRepAssignDto dto)
        {
            var federation = await _db.Federations
                .Include(f => f.Province).Include(f => f.City).ThenInclude(c => c!.Province)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            if (!CanManageFederationRepScope(federation))
                return Forbid("شما اجازه مدیریت نمایندگان فurasیون در این سطح را ندارید");

            if (string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest(new { detail = "نام کاربری الزامی است" });

            if (!Enum.TryParse<FederationPositionType>(dto.Position, out var positionEnum) ||
                !Enum.IsDefined(positionEnum))
                return BadRequest(new { detail = "سمت انتخاب‌شده معتبر نیست" });

            var username = dto.Username.Trim();
            var user = await _userManager.FindByNameAsync(username);
            var isNewUser = user == null;
            if (user == null)
            {
                if (string.IsNullOrWhiteSpace(dto.Password))
                    return BadRequest(new { detail = "برای ایجاد حساب جدید، کلمه عبور الزامی است" });

                var scopeLevel = federation.Level switch
                {
                    FederationLevel.National => ScopeLevel.Mastery,
                    FederationLevel.Provincial => ScopeLevel.Province,
                    FederationLevel.City => ScopeLevel.City,
                    _ => throw new InvalidOperationException("Unsupported federation level.")
                };
                user = new ApplicationUser
                {
                    UserName = username,
                    Email = $"{username}@local",
                    FirstName = dto.FirstName ?? "",
                    LastName = dto.LastName ?? "",
                    Role = UserRole.FederationRep,
                    ScopeLevel = scopeLevel,
                    ScopeProvinceId = federation.Level == FederationLevel.City
                        ? federation.City?.ProvinceId
                        : federation.ProvinceId,
                    ScopeCityId = federation.CityId,
                    EmailConfirmed = true
                };
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();
            if (isNewUser)
            {
                var createResult = await _userManager.CreateAsync(user!, dto.Password!);
                if (!createResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var errors = string.Join(" / ", createResult.Errors.Select(e => e.Description));
                    return BadRequest(new { detail = errors });
                }
            }

            var (position, error) = await _federationPositionService.AssignAsync(
                user!, federation, positionEnum, dto.StartDate, dto.EndDate);
            if (error != null)
            {
                await transaction.RollbackAsync();
                return Conflict(new { detail = error });
            }

            await transaction.CommitAsync();
            return CreatedAtAction(nameof(GetFederationRep), new { id, userId = user!.Id },
                new { userId = user.Id, username = user.UserName, positionId = position!.Id });
        }

        // Update FederationRep account
        [HttpPut("{id}/federation-reps/{userId}")]
        public async Task<IActionResult> UpdateFederationRep(Guid id, Guid userId, UserUpdateDto dto)
        {
            var federation = await _db.Federations.FindAsync(id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound(new { detail = "کاربر یافت نشد" });

            if (!CanManageFederationRepScope(federation))
                return Forbid("شما اجازه ویرایش این نماینده فurasیون را ندارید");

            if (!await _db.FederationPositions.AnyAsync(p => p.FederationId == id && p.UserId == userId))
                return NotFound(new { detail = "نماینده فدراسیون یافت نشد" });

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(" / ", result.Errors.Select(e => e.Description));
                return BadRequest(new { detail = errors });
            }

            return Ok(new { success = true });
        }

        // End this user's assignments in the selected federation without disabling
        // their account or affecting positions held in other federations.
        [HttpDelete("{id}/federation-reps/{userId}")]
        public async Task<IActionResult> DeactivateFederationRep(Guid id, Guid userId)
        {
            var federation = await _db.Federations.FindAsync(id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound(new { detail = "کاربر یافت نشد" });

            if (!CanManageFederationRepScope(federation))
                return Forbid("شما اجازه غیرفعال‌سازی این نماینده فurasیون را ندارید");

            var today = TehranDateTime.TodayUtc;
            var assignments = await _db.FederationPositions
                .Where(p => p.FederationId == id && p.UserId == userId && p.StartDate <= today)
                .ToListAsync();
            if (assignments.Count == 0)
                return NotFound(new { detail = "نماینده فدراسیون یافت نشد" });

            foreach (var assignment in assignments)
            {
                var effectiveEndDate = assignment.EndDate ??
                    FederationPositionService.GetTermEndDate(assignment.StartDate);
                if (effectiveEndDate > today)
                    assignment.EndDate = today;
            }
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        private static UserOutDto ToFederationRepOutDto(ApplicationUser user, Federation federation)
        {
            var scopeLevel = federation.Level switch
            {
                FederationLevel.National => ScopeLevel.Mastery,
                FederationLevel.Provincial => ScopeLevel.Province,
                FederationLevel.City => ScopeLevel.City,
                _ => throw new InvalidOperationException("Unsupported federation level.")
            };

            return new UserOutDto(
                user.Id, user.UserName!, user.FirstName ?? "", user.LastName ?? "",
                UserRole.FederationRep.ToString(), scopeLevel.ToString(),
                federation.Level == FederationLevel.City ? federation.City?.ProvinceId : federation.ProvinceId,
                federation.Province?.Name ?? federation.City?.Province?.Name,
                federation.CityId, federation.City?.Name);
        }

        // Check if current user can manage FederationRep accounts for this federation
        private bool CanManageFederationRepScope(Federation federation)
        {
            var myRole = User.Role();
            var myScopeLevel = User.ScopeLevel();

            // Admin, Manager, SportsManager can manage FederationRep accounts
            if (myRole is not ("Admin" or "Manager" or "SportsManager"))
                return false;

            // Scope validation based on federation level
            if (federation.Level == FederationLevel.National)
            {
                // Only Mastery can manage national federation reps
                return myScopeLevel == "Mastery";
            }
            
            if (federation.Level == FederationLevel.Provincial)
            {
                if (myScopeLevel == "Mastery") return true;
                if (myScopeLevel == "Province") return User.ScopeProvinceId() == federation.ProvinceId;
                return false;
            }
            
            if (federation.Level == FederationLevel.City)
            {
                if (myScopeLevel == "Mastery") return true;
                if (myScopeLevel == "Province") return User.ScopeProvinceId() == federation.ProvinceId;
                if (myScopeLevel == "City") return User.ScopeCityId() == federation.CityId;
                return false;
            }

            return false;
        }
    }
}
