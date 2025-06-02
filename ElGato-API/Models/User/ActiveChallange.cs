using ElGato_API.Models.Feed;
using System.ComponentModel.DataAnnotations;

namespace ElGato_API.Models.User
{
    public class ActiveChallange
    {
        [Key]
        public int Id { get; set; }
        public Challange Challenge { get; set; }
        public int ChallengeId { get; set; }
        public double CurrentProgress { get; set; }
        public DateTime StartDate { get; set; }
        public string UserId { get; set; }
        public AppUser User { get; set; }
    }
}
