using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.VMO.Achievments
{
    public class AchievmentResponse
    {
        public AchievmentVMO? Achievment { get; set; }
        public BasicErrorResponse Status { get; set; }
    }

    public class AchievmentVMO 
    {
        public string AchievmentEarnedName { get; set; }
        public string AchievmentEarnedImage { get; set; }
        public string GenerativeText { get; set; }
        public int ExceededThreshold { get; set; }
    }

}
