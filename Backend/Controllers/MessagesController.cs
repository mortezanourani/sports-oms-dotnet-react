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
    [Authorize] // every action requires login; no role restrictions beyond that
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
        // Sends a new message to one or more recipients. No role restriction
        // — every logged-in user can send, per the spec.
        [HttpPost]
        public async Task<IActionResult> Send(MessageSendDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Body))
                return BadRequest(new { detail = "متن پیام نمی‌تواند خالی باشد" });

            if (dto.RecipientIds == null || dto.RecipientIds.Count == 0)
                return BadRequest(new { detail = "حداقل یک گیرنده باید انتخاب شود" });

            // Confirm every submitted recipient ID actually corresponds to a
            // real user — without this check, a typo'd or stale ID would
            // silently create a MessageRecipient row pointing at nothing
            // meaningful, and that recipient would never see the message
            // with no error ever surfacing to the sender.
            var validRecipientIds = await _db.Users
                .Where(u => dto.RecipientIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();

            var invalidIds = dto.RecipientIds.Except(validRecipientIds).ToList();
            if (invalidIds.Count > 0)
                return BadRequest(new { detail = "برخی از گیرندگان انتخاب‌شده معتبر نیستند" });

            var message = new Message
            {
                SenderId = CurrentUserId,
                Subject = dto.Subject,
                Body = dto.Body
            };

            _db.Messages.Add(message);

            foreach (var recipientId in validRecipientIds.Distinct())
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
        // Populates the "select recipients" picker in the compose UI. No
        // role restriction, since every user needs to be able to message
        // others — but deliberately excludes the current user themselves
        // from their own recipient list, since messaging yourself has no
        // real use case here.
        [HttpGet("recipients")]
        public async Task<IActionResult> RecipientOptions()
        {
            var myId = CurrentUserId;

            var users = await _db.Users
                .Where(u => u.Id != myId)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new RecipientOptionDto(u.Id, GetDisplayName(u), u.Role.ToString()))
                .ToListAsync();

            return Ok(users);
        }
    }
}