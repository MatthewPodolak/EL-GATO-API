using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.Community
{
    public class RespondToFollowVM
    {
        [Required(ErrorMessage = "Request id is required")]
        public int RequestId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public string RequestingUserId { get; set; }
        public FollowRequestDecision Decision { get; set; } = FollowRequestDecision.Accept;
    }

    public enum FollowRequestDecision 
    { 
        Remove,
        Accept
    }

}
