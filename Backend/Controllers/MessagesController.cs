using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Net.Mime;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/messages")]
    [Authorize] // all actions require login; recipient permissions are checked when sending
    public class MessagesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly FileStorageService _fileStorage;
        public MessagesController(ApplicationDbContext db, FileStorageService fileStorage)
        {
            _db = db;
            _fileStorage = fileStorage;
        }

        private static string GetDisplayName(ApplicationUser user)
        {
            var parts = new string?[] { user.FirstName, user.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim())
                .ToArray();

            return parts.Length > 0
                ? string.Join(" ", parts)
                : user.UserName ?? "کاربر";
        }

        // Small helper to get the logged-in user's own ID out of the JWT
        // claims, used throughout this controller to filter to "my" messages.
        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        // GET /api/messages/inbox
        // Returns messages where the current user is a recipient — this is
        // the core privacy boundary of the whole controller: a user can only
        // ever query messages tied to their own ID, never an arbitrary one.
        [HttpGet("inbox")]
        public async Task<IActionResult> Inbox(int page = 1, int pageSize = 20)
        {
            var myId = CurrentUserId;

            var qs = _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipients)
                .Include(m => m.Attachments)
                .Where(m => m.Recipients.Any(r => r.RecipientId == myId));

            var count = await qs.CountAsync();
            var items = await qs
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MessageListOutDto(
                    m.Id, m.Subject,
                    m.Body.Length > 100 ? m.Body.Substring(0, 100) + "…" : m.Body,
                    m.CreatedAt, m.SenderId, GetDisplayName(m.Sender),
                    m.Recipients.First(r => r.RecipientId == myId).IsRead,
                    m.Recipients.Count, m.Attachments.Count))
                .ToListAsync();

            return Ok(new { items, count });
        }

        // GET /api/messages/sent
        // Mirror of Inbox, but filtered to messages the current user sent.
        [HttpGet("sent")]
        public async Task<IActionResult> Sent(int page = 1, int pageSize = 20)
        {
            var myId = CurrentUserId;

            var qs = _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipients)
                .Include(m => m.Attachments)
                .Where(m => m.SenderId == myId);

            var count = await qs.CountAsync();
            var items = await qs
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MessageListOutDto(
                    m.Id, m.Subject,
                    m.Body.Length > 100 ? m.Body.Substring(0, 100) + "…" : m.Body,
                    m.CreatedAt, m.SenderId, GetDisplayName(m.Sender),
                    true, // IsRead is meaningless for a message you sent yourself — always true
                    m.Recipients.Count, m.Attachments.Count))
                .ToListAsync();

            return Ok(new { items, count });
        }

        // GET /api/messages/{id}
        // Opening a specific message. Two things happen here beyond a normal
        // "get by id": (1) an access check confirming the current user is
        // either the sender or a recipient — otherwise this endpoint would
        // let anyone read anyone else's private messages just by guessing or
        // incrementing IDs; (2) if the current user is a recipient, this call
        // marks it as read, since "opening a message" is the natural,
        // implicit trigger for that — no separate "mark as read" button/
        // endpoint needed for the common case.
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var myId = CurrentUserId;

            var message = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipients).ThenInclude(r => r.Recipient)
                .Include(m => m.Attachments)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (message == null)
                return NotFound(new { detail = "پیام یافت نشد" });

            var myRecipientRow = message.Recipients.FirstOrDefault(r => r.RecipientId == myId);
            var isSender = message.SenderId == myId;

            if (!isSender && myRecipientRow == null)
                return Forbid("شما اجازه مشاهده این پیام را ندارید");

            if (myRecipientRow != null && !myRecipientRow.IsRead)
            {
                myRecipientRow.IsRead = true;
                myRecipientRow.ReadAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok(new MessageDetailOutDto(
                message.Id, message.Subject, message.Body, message.CreatedAt,
                message.SenderId, GetDisplayName(message.Sender),
                message.Recipients.Select(r => new MessageRecipientOutDto(
                    r.RecipientId, GetDisplayName(r.Recipient), r.IsRead, r.ReadAt)).ToList(),
                message.Attachments.Select(a => new MessageAttachmentOutDto(
                    a.Id, System.IO.Path.GetFileName(a.FilePath), a.UploadedAt)).ToList()));
        }

        [HttpPost("{id}/attachments")]
        public async Task<IActionResult> AddAttachment(Guid id, IFormFile file)
        {
            var myId = CurrentUserId; var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == id && m.SenderId == myId); // Deliberately restricted to the SENDER only — a recipient shouldn't be // able to add files to a message after the fact; that would let someone // silently inject content into a report the recipient already opened // and trusted. if (message == null) return NotFound(new { detail = "پیام یافت نشد یا شما فرستنده آن نیستید" });
            var (success, relativePath, error) = await _fileStorage.SaveAsync(file, "messages", isPublic: false, isImage: false);
            if (!success) return BadRequest(new { detail = error });

            var attachment = new MessageAttachment { MessageId = id, FilePath = relativePath! };
            _db.MessageAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            return Ok(new { id = attachment.Id, fileName = file.FileName });
        }

        // GET /api/messages/attachments/{attachmentId}/download 
        // This is the private-file access-control gate mentioned in Step 3 — the 
        // ONLY way a private file is ever served. It re-checks the exact same 
        // "are you the sender or a recipient of the parent message" rule used in 
        // Get(id) above, meaning an attachment is exactly as protected as the 
        // message it belongs to, never more loosely.
        [HttpGet("attachments/{attachmentId}/download")]
        public async Task<IActionResult> DownloadAttachment(Guid attachmentId)
        {
            var myId = CurrentUserId;
            var attachment = await _db.MessageAttachments
                .Include(a => a.Message).ThenInclude(m => m.Recipients)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);

            if (attachment == null) return NotFound();

            var isSender = attachment.Message.SenderId == myId;
            var isRecipient = attachment.Message.Recipients.Any(r => r.RecipientId == myId);
            if (!isSender && !isRecipient)
                return Forbid("شما اجازه دسترسی به این فایل را ندارید");

            var fullPath = _fileStorage.GetFullPath(attachment.FilePath);
            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { detail = "فایل روی سرور یافت نشد" });

            var contentType = "application/octet-stream";
            // generic; browser will prompt to download
            return PhysicalFile(fullPath, contentType, Path.GetFileName(attachment.FilePath));
        }

        // POST /api/messages
        // Sends a message only to recipients allowed by the sender's role and scope.
        [HttpPost]
        public async Task<IActionResult> Send(MessageSendDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Body))
                return BadRequest(new { detail = "متن پیام نمی‌تواند خالی باشد" });

            if (dto.RecipientIds == null || dto.RecipientIds.Count == 0)
                return BadRequest(new { detail = "حداقل یک گیرنده باید انتخاب شود" });

            var sender = await _db.Users.FindAsync(CurrentUserId);
            if (sender == null) return Unauthorized();

            var allowedRecipientIds = await GetAllowedRecipientIdsAsync(
                sender, dto.RecipientType, dto.ScopeLevel);
            var recipientIds = dto.RecipientIds.Distinct().ToList();
            if (recipientIds.Any(id => !allowedRecipientIds.Contains(id)))
                return BadRequest(new { detail = "انتخاب یک یا چند گیرنده مجاز نیست" });

            var message = new Message
            {
                SenderId = sender.Id,
                Subject = dto.Subject,
                Body = dto.Body
            };

            _db.Messages.Add(message);

            foreach (var recipientId in recipientIds)
            {
                _db.MessageRecipients.Add(new MessageRecipient
                {
                    Message = message,
                    RecipientId = recipientId
                });
            }
            // .Distinct() guards against the same ID appearing twice in the
            // submitted list — without it, a duplicate would violate the
            // unique index on (MessageId, RecipientId) set up in
            // ApplicationDbContext, causing the whole send to fail with a
            // raw database error instead of just silently deduplicating,
            // which is the more forgiving and expected behavior here.

            await _db.SaveChangesAsync();

            return Ok(new { id = message.Id });
        }

        // GET /api/messages/unread-count
        // A small dedicated endpoint for showing a notification badge
        // (e.g. "3" next to the Messages sidebar link) without the frontend
        // needing to fetch and count the entire inbox just to get one number.
        [HttpGet("unread-count")]
        public async Task<IActionResult> UnreadCount()
        {
            var myId = CurrentUserId;
            var count = await _db.MessageRecipients
                .CountAsync(r => r.RecipientId == myId && !r.IsRead);

            return Ok(new { unreadCount = count });
        }

        // GET /api/messages/recipients
        // Returns recipients allowed for the selected receiver type and scope.
        [HttpGet("recipients")]
        public async Task<IActionResult> RecipientOptions(
            [FromQuery] string recipientType,
            [FromQuery] string? scopeLevel = null)
        {
            var sender = await _db.Users.FindAsync(CurrentUserId);
            if (sender == null) return Unauthorized();

            var allowedRecipientIds = await GetAllowedRecipientIdsAsync(
                sender, recipientType, scopeLevel);

            var users = await _db.Users
                .Where(u => allowedRecipientIds.Contains(u.Id))
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToListAsync();

            var federationPositionLabels = recipientType == "FederationRep"
                ? await GetAllowedFederationPositionLabelsAsync(sender, allowedRecipientIds)
                : new Dictionary<Guid, List<string>>();

            return Ok(users.Select(user => new RecipientOptionDto(
                user.Id,
                GetDisplayName(user),
                user.Role.ToString(),
                user.ScopeLevel.ToString(),
                user.ScopeProvince?.Name,
                user.ScopeCity?.Name,
                federationPositionLabels.GetValueOrDefault(user.Id, []))));
        }

        private async Task<List<Guid>> GetAllowedRecipientIdsAsync(
            ApplicationUser sender, string recipientType, string? scopeLevel)
        {
            if (recipientType == "MainRole")
            {
                if (sender.Role == UserRole.FederationRep)
                    return await GetFederationPresidentMainRoleRecipientsAsync(sender);

                if (!Enum.TryParse<ScopeLevel>(scopeLevel, out var recipientScope) ||
                    !Enum.IsDefined(recipientScope))
                    return [];

                var candidates = await _db.Users
                    .Where(user => user.Id != sender.Id && user.ScopeLevel == recipientScope)
                    .ToListAsync();

                return candidates
                    .Where(user => CanMessageMainRoleUser(sender, user))
                    .Select(user => user.Id)
                    .ToList();
            }

            if (recipientType != "FederationRep")
                return [];

            var today = TehranDateTime.TodayUtc;
            var senderPositions = await _db.FederationPositions
                .Include(position => position.Federation)
                .Where(position => position.UserId == sender.Id &&
                                   position.User.Role == UserRole.FederationRep &&
                                   position.StartDate <= today)
                .ToListAsync();
            senderPositions = senderPositions
                .Where(position => IsActivePosition(position, today))
                .ToList();

            if (sender.Role != UserRole.FederationRep)
            {
                if (sender.Role is not (UserRole.Admin or UserRole.Manager or UserRole.SportsManager))
                    return [];

                var positions = await _db.FederationPositions
                    .Include(position => position.User)
                    .Include(position => position.Federation)
                    .ThenInclude(federation => federation.City)
                    .Where(position =>
                        position.User.Role == UserRole.FederationRep &&
                        position.StartDate <= today)
                    .ToListAsync();

                return positions
                    .Where(position =>
                        IsActivePosition(position, today) &&
                        IsFederationWithinGeographicScope(position.Federation, sender) &&
                        (sender.Role != UserRole.SportsManager ||
                         position.Position == FederationPositionType.President))
                    .Select(position => position.UserId)
                    .Where(id => id != sender.Id)
                    .Distinct()
                    .ToList();
            }

            var senderFederationIds = senderPositions
                .Select(position => position.FederationId)
                .Distinct()
                .ToList();
            if (senderFederationIds.Count == 0) return [];

            var allowedIds = new HashSet<Guid>();
            var sameFederationPositions = await GetActivePositionsAsync(
                _db.FederationPositions
                    .Include(position => position.User)
                    .Where(position =>
                        senderFederationIds.Contains(position.FederationId) &&
                        position.User.Role == UserRole.FederationRep &&
                        position.StartDate <= today),
                today);

            foreach (var position in sameFederationPositions)
                if (position.UserId != sender.Id)
                    allowedIds.Add(position.UserId);

            var presidentFederationIds = senderPositions
                .Where(position => position.Position == FederationPositionType.President)
                .Select(position => position.FederationId)
                .Distinct()
                .ToList();
            if (presidentFederationIds.Count == 0) return allowedIds.ToList();

            var adjacentFederationIds = senderPositions
                .Where(position => presidentFederationIds.Contains(position.FederationId))
                .Select(position => position.Federation.ParentFederationId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            var childFederationIds = await _db.Federations
                .Where(federation =>
                    federation.ParentFederationId.HasValue &&
                    presidentFederationIds.Contains(federation.ParentFederationId.Value))
                .Select(federation => federation.Id)
                .ToListAsync();
            adjacentFederationIds.UnionWith(childFederationIds);

            if (adjacentFederationIds.Count == 0) return allowedIds.ToList();

            var adjacentPresidentPositions = await GetActivePositionsAsync(
                _db.FederationPositions
                    .Include(position => position.User)
                    .Where(position =>
                        adjacentFederationIds.Contains(position.FederationId) &&
                        position.Position == FederationPositionType.President &&
                        position.User.Role == UserRole.FederationRep &&
                        position.StartDate <= today),
                today);
            foreach (var position in adjacentPresidentPositions)
                if (position.UserId != sender.Id)
                    allowedIds.Add(position.UserId);

            return allowedIds.ToList();
        }

        private async Task<List<Guid>> GetFederationPresidentMainRoleRecipientsAsync(
            ApplicationUser sender)
        {
            var today = TehranDateTime.TodayUtc;
            var positions = await _db.FederationPositions
                .Include(position => position.Federation)
                .Where(position =>
                    position.UserId == sender.Id &&
                    position.User.Role == UserRole.FederationRep &&
                    position.Position == FederationPositionType.President &&
                    position.StartDate <= today)
                .ToListAsync();
            var hasActivePresidentPosition = positions.Any(position => IsActivePosition(position, today));
            if (!hasActivePresidentPosition) return [];

            var users = await _db.Users
                .Where(user =>
                    user.Id != sender.Id &&
                    (user.Role == UserRole.Admin ||
                     user.Role == UserRole.Manager ||
                     user.Role == UserRole.SportsManager))
                .ToListAsync();

            return users
                .Where(user => IsSameGeographicScope(user, sender))
                .Select(user => user.Id)
                .ToList();
        }

        private async Task<Dictionary<Guid, List<string>>> GetAllowedFederationPositionLabelsAsync(
            ApplicationUser sender, List<Guid> allowedRecipientIds)
        {
            if (allowedRecipientIds.Count == 0) return [];

            var today = TehranDateTime.TodayUtc;
            var senderPositions = await _db.FederationPositions
                .Where(position =>
                    position.UserId == sender.Id &&
                    position.User.Role == UserRole.FederationRep &&
                    position.StartDate <= today)
                .ToListAsync();
            senderPositions = senderPositions
                .Where(position => IsActivePosition(position, today))
                .ToList();

            IQueryable<FederationPosition> positionsQuery = _db.FederationPositions
                .Include(position => position.Federation)
                    .ThenInclude(federation => federation.Province)
                .Include(position => position.Federation)
                    .ThenInclude(federation => federation.ParentFederation)
                        .ThenInclude(parent => parent!.ParentFederation)
                .Include(position => position.Federation)
                    .ThenInclude(federation => federation.City)
                        .ThenInclude(city => city!.Province)
                .Where(position =>
                    allowedRecipientIds.Contains(position.UserId) &&
                    position.User.Role == UserRole.FederationRep &&
                    position.StartDate <= today);

            if (sender.Role != UserRole.FederationRep)
            {
                var positions = await positionsQuery.ToListAsync();
                return positions
                    .Where(position =>
                        IsActivePosition(position, today) &&
                        IsFederationWithinGeographicScope(position.Federation, sender) &&
                        (sender.Role != UserRole.SportsManager ||
                         position.Position == FederationPositionType.President))
                    .GroupBy(position => position.UserId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(FormatFederationPositionLabel).Distinct().ToList());
            }

            var senderFederationIds = senderPositions
                .Select(position => position.FederationId)
                .ToHashSet();
            var presidentFederationIds = senderPositions
                .Where(position => position.Position == FederationPositionType.President)
                .Select(position => position.FederationId)
                .ToHashSet();
            var adjacentFederationIds = senderPositions
                .Where(position => presidentFederationIds.Contains(position.FederationId))
                .Select(position => position.Federation?.ParentFederationId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
            var childFederationIds = await _db.Federations
                .Where(federation =>
                    federation.ParentFederationId.HasValue &&
                    presidentFederationIds.Contains(federation.ParentFederationId.Value))
                .Select(federation => federation.Id)
                .ToListAsync();
            adjacentFederationIds.UnionWith(childFederationIds);

            var recipientPositions = await positionsQuery.ToListAsync();
            return recipientPositions
                .Where(position =>
                    IsActivePosition(position, today) &&
                    (senderFederationIds.Contains(position.FederationId) ||
                     adjacentFederationIds.Contains(position.FederationId) &&
                     position.Position == FederationPositionType.President))
                .GroupBy(position => position.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(FormatFederationPositionLabel).Distinct().ToList());
        }

        private static string FormatFederationPositionLabel(FederationPosition position)
        {
            var federation = position.Federation;
            var positionName = position.Position switch
            {
                FederationPositionType.President => "رئیس",
                FederationPositionType.Miss => "نایب رئیس",
                FederationPositionType.Vice => "دبیر",
                _ => position.Position.ToString()
            };
            var federationName = federation.Level == FederationLevel.National
                ? $"فدراسیون {federation.Name}"
                : federation.Level == FederationLevel.Provincial
                    ? $"هیات {federation.ParentFederation?.Name} استان {federation.Province?.Name}"
                    : $"هیات {federation.ParentFederation?.ParentFederation?.Name} شهرستان {federation.City?.Name}";

            return $"{positionName} {federationName}";
        }

        private static bool CanMessageMainRoleUser(ApplicationUser sender, ApplicationUser recipient)
        {
            if (recipient.Role == UserRole.FederationRep) return false;

            if (sender.Role == UserRole.FederationRep)
                return false;

            var isManagerOrAdmin = sender.Role is UserRole.Manager or UserRole.Admin;
            var isOfficer = sender.Role is UserRole.SportsOfficer or UserRole.M5Officer or
                UserRole.M88Officer or UserRole.HumanResourceOfficer;
            var isOfficerRecipientExclusion =
                recipient.Role == UserRole.Admin || IsManagerRole(recipient.Role);
            var sameOrDescendantScope = IsSameGeographicScope(recipient, sender) ||
                                        IsGeographicDescendantScope(recipient, sender);

            if (isOfficer && isOfficerRecipientExclusion)
                return false;

            if (sameOrDescendantScope)
                return true;

            if (!IsAdjacentGeographicScope(recipient, sender))
                return false;

            if (isManagerOrAdmin)
                return recipient.Role is UserRole.Manager or UserRole.Admin;

            return recipient.Role == sender.Role &&
                   sender.Role is UserRole.SportsManager or UserRole.M5Manager or
                       UserRole.M88Manager or UserRole.HumanResourceManager or
                       UserRole.InsuranceOfficer;
        }

        private static bool IsManagerRole(UserRole role) =>
            role is UserRole.Manager or UserRole.SportsManager or UserRole.M5Manager or
                UserRole.M88Manager or UserRole.HumanResourceManager;

        private static bool IsSameGeographicScope(ApplicationUser recipient, ApplicationUser sender) =>
            recipient.ScopeLevel == sender.ScopeLevel &&
            sender.ScopeLevel switch
            {
                ScopeLevel.Mastery => true,
                ScopeLevel.Province => sender.ScopeProvinceId.HasValue &&
                                       recipient.ScopeProvinceId == sender.ScopeProvinceId,
                ScopeLevel.City => sender.ScopeCityId.HasValue &&
                                   recipient.ScopeCityId == sender.ScopeCityId,
                _ => false
            };

        private static bool IsGeographicDescendantScope(ApplicationUser recipient, ApplicationUser sender) =>
            sender.ScopeLevel == ScopeLevel.Province &&
            recipient.ScopeLevel == ScopeLevel.City &&
            sender.ScopeProvinceId.HasValue &&
            recipient.ScopeProvinceId == sender.ScopeProvinceId;

        private static bool IsAdjacentGeographicScope(ApplicationUser recipient, ApplicationUser sender) =>
            (sender.ScopeLevel == ScopeLevel.Mastery && recipient.ScopeLevel == ScopeLevel.Province) ||
            (sender.ScopeLevel == ScopeLevel.Province && recipient.ScopeLevel == ScopeLevel.Mastery) ||
            (sender.ScopeLevel == ScopeLevel.Province &&
             recipient.ScopeLevel == ScopeLevel.City &&
             sender.ScopeProvinceId.HasValue &&
             recipient.ScopeProvinceId == sender.ScopeProvinceId) ||
            (sender.ScopeLevel == ScopeLevel.City &&
             recipient.ScopeLevel == ScopeLevel.Province &&
             sender.ScopeProvinceId.HasValue &&
             recipient.ScopeProvinceId == sender.ScopeProvinceId);

        private static bool IsFederationWithinGeographicScope(
            Federation federation, ApplicationUser sender) =>
            sender.ScopeLevel switch
            {
                ScopeLevel.Mastery => true,
                ScopeLevel.Province => sender.ScopeProvinceId.HasValue &&
                    (federation.Level == FederationLevel.Provincial &&
                     federation.ProvinceId == sender.ScopeProvinceId ||
                     federation.Level == FederationLevel.City &&
                     federation.City?.ProvinceId == sender.ScopeProvinceId),
                ScopeLevel.City => sender.ScopeCityId.HasValue &&
                    federation.Level == FederationLevel.City &&
                    federation.CityId == sender.ScopeCityId,
                _ => false
            };

        private async Task<List<FederationPosition>> GetActivePositionsAsync(
            IQueryable<FederationPosition> query, DateTime today)
        {
            var positions = await query.ToListAsync();
            return positions.Where(position => IsActivePosition(position, today)).ToList();
        }

        private static bool IsActivePosition(FederationPosition position, DateTime today) =>
            (position.EndDate ?? FederationPositionService.GetTermEndDate(position.StartDate)) > today;

    }
}