using ElGato_API.VM.Requests;

namespace ElGato_API.Models.Requests
{
    public class ReportedUsers
    {
        public int Id { get; set; }
        public string ReportedUserId { get; set; }
        public string ReportingUserId { get; set; }
        public UserReportCase Case { get; set; } = UserReportCase.Other;
        public string CaseDescription { get; set; } = string.Empty;
    }
}
