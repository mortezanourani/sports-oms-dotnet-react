using System.ComponentModel.DataAnnotations;

namespace Backend.Models
{
    public enum JobPostManagerType
    {
        [Display(Name = "نامشخص")]
        Unspecified,
        [Display(Name = "مدیر پایه")]
        BasicManager,
        [Display(Name = "مدیر میانی")]
        MiddleManager,
        [Display(Name = "مدیر ارشد")]
        SeniorManager
    }

    public class JobPost
    {
        public Guid Id { get; set; }

        public Guid JobPostTypeId { get; set; }
        public JobPostType JobPostType { get; set; } = null!;

        public Guid OrgUnitId { get; set; }
        public OrgUnit OrgUnit { get; set; } = null!;

        public string TypeCode { get; set; } = string.Empty;
        public string JobPostCode { get; set; } = string.Empty;
        public string JobPostUniqueId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public JobPostManagerType ManagerType { get; set; }
        public bool? BreakOut { get; set; }
    }
}
