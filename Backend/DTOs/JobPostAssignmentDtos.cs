namespace Backend.DTOs;

public record JobPostAssignmentCreateDto(
    Guid UserId,
    Guid JobPostId,
    DateOnly? StartDate,
    DateOnly? EndDate);

public record JobPostAssignmentEndDto(DateOnly EndDate);

public record JobPostAssignmentOutDto(
    Guid Id,
    Guid UserId,
    string Username,
    string UserFirstName,
    string UserLastName,
    Guid JobPostId,
    string JobPostTitle,
    string JobPostUniqueId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTimeOffset CreatedAtUtc);

public record JobPostAssignmentPostDto(Guid Id, string Title, string JobPostUniqueId);

public record JobPostAssignmentImportErrorDto(int RowNumber, string Reason);

public record JobPostAssignmentImportResultDto(
    int Created,
    int Failed,
    IReadOnlyList<JobPostAssignmentImportErrorDto> Errors);
