namespace Backend.DTOs
{
    public record MessageSendDto(string? Subject, string Body, List<Guid> RecipientIds);
    // Used in inbox/sent list views — no full body text, keeps the list
    // response light. A list of 50 messages each carrying full body text
    // (which could include long report content) would be unnecessarily
    // heavy for a page that's just meant to show subject + sender + date.
    public record MessageListOutDto(
        Guid Id, string? Subject, string BodyPreview, DateTime CreatedAt,
        Guid SenderId, string SenderName, bool IsRead, int RecipientCount, int AttachmentCount);

    // Full detail — used when actually opening a specific message.
    public record MessageDetailOutDto(
        Guid Id, string? Subject, string Body, DateTime CreatedAt,
        Guid SenderId, string SenderName,
        List<MessageRecipientOutDto> Recipients,
        List<MessageAttachmentOutDto> Attachments);

    public record MessageRecipientOutDto(Guid UserId, string UserName, bool IsRead, DateTime? ReadAt);

    public record MessageAttachmentOutDto(Guid Id, string FileName, DateTime UploadedAt);

    // A simple lookup DTO for populating the recipient picker in the compose
    // UI — deliberately minimal, just enough to display and select a person.
    public record RecipientOptionDto(Guid Id, string FullName, string Role);
}
