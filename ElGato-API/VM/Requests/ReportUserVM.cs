using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.Requests
{
    public class ReportUserVM
    {
        [Required(ErrorMessage = "UserId is required")]
        public string ReportedUserId { get; set; }
        public UserReportCase ReportCase { get; set; } = UserReportCase.Other;
        public string? OtherDescription { get; set; }
    }

    public enum UserReportCase
    { 
        Other,
        VulgarUserName,
        VulgarProfilePicture,
        Racism,
        Abuse
    }
}
