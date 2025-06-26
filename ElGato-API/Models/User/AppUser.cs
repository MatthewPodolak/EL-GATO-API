using ElGato_API.Models.Requests;
using Microsoft.AspNetCore.Identity;

namespace ElGato_API.Models.User
{
    public class AppUser : IdentityUser
    {
        public string? Name { get; set; }
        public string? Desc { get; set; }
        public bool Metric { get; set; } = true;
        public string Pfp { get; set; } = "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png";
        public int StepsThreshold { get; set; } = 3000;
        public UserInformation? UserInformation { get; set; }
        public CalorieInformation? CalorieInformation { get; set; }
        public List<Achievment>? Achievments { get; set; } = new List<Achievment>();
        public List<AchievementCounter> AchivmentCounter { get; set; } = new List<AchievementCounter>();
        public List<ActiveChallange>? ActiveChallanges { get; set; } = new List<ActiveChallange>();
        public List<UserBadges>? UserBadges { get; set; } = new List<UserBadges>();
        public int FollowersCount { get; set; } = 0;
        public int FollowingCount { get; set; } = 0;
        public bool IsProfilePrivate { get; set; } = false;
        public List<UserFollower> Followers { get; set; } = new List<UserFollower>();
        public List<UserFollower> Following { get; set; } = new List<UserFollower>();
        public List<UserBlock> BlockedUsers { get; set; } = new List<UserBlock>();
        public List<UserBlock> BlockedByUsers { get; set; } = new List<UserBlock>();
        public List<UserFollowerRequest> ReceivedFollowRequests { get; set; } = new List<UserFollowerRequest>();
        public List<UserFollowerRequest> SentFollowRequests { get; set; } = new List<UserFollowerRequest>();
        public LayoutSettings? LayoutSettings { get; set; } = new LayoutSettings
        {
            Animations = true,
            ChartStack = new List<ChartStack>
            {
                new ChartStack
                {
                    ChartType = ChartType.Linear,
                    ChartDataType = ChartDataType.Exercise,
                    Period = Period.All,
                    Name = "Benchpress"
                },
                new ChartStack
                {
                    ChartType = ChartType.Compare,
                    ChartDataType = ChartDataType.Exercise,
                    Period = Period.Last,
                    Name = "Benchpress"
                },
                new ChartStack
                {
                    ChartType = ChartType.Hexagonal,
                    ChartDataType = ChartDataType.NotDefined,
                    Period = Period.Week,
                    Name = "Muscle engagement"
                },
                new ChartStack
                {
                    ChartType = ChartType.Bar,
                    ChartDataType = ChartDataType.Calorie,
                    Period = Period.Last5,
                    Name = "Calories"
                },
                new ChartStack
                {
                    ChartType = ChartType.Circle,
                    ChartDataType = ChartDataType.MakroDist,
                    Period = Period.Last,
                    Name = "Daily makro"
                }
            }
        };
    }
}
