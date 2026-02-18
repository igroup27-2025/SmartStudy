namespace SmartStudy.DTOs;

public class SharedTaskDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = null!;
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public string CreatedByEmail { get; set; } = null!;
    public string CreatedByName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string SharedStatus { get; set; } = null!;
    public List<SharedTaskMemberDto> Members { get; set; } = new();
}

public class SharedTaskMemberDto
{
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string ResponseStatus { get; set; } = null!;
    public DateTime? RespondedAt { get; set; }
}

public class CreateSharedTaskDto
{
    public int TaskId { get; set; }
    public string PartnerEmail { get; set; } = null!;
}

public class RespondSharedTaskDto
{
    public bool Accept { get; set; }
}
