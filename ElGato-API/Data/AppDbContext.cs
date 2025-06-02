using ElGato_API.Migrations;
using ElGato_API.Models.Feed;
using ElGato_API.Models.Requests;
using ElGato_API.Models.Training;
using ElGato_API.Models.User;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.Json;

namespace ElGato_API.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Name)
                .IsUnique(false);

            modelBuilder.Entity<AppUser>()
             .HasOne(a => a.UserInformation)
             .WithOne(u => u.AppUser)
             .HasForeignKey<UserInformation>(ui => ui.UserId);

            modelBuilder.Entity<AppUser>()
             .HasOne(a => a.CalorieInformation)
             .WithOne(u => u.AppUser)
             .HasForeignKey<CalorieInformation>(ui => ui.UserId);

            modelBuilder.Entity<AppUser>()
                .HasMany(a => a.Achievments)
                .WithMany(a => a.Users)
                .UsingEntity(j => j.ToTable("UserAchievements"));

            modelBuilder.Entity<AchievementCounter>()
                .HasOne(ac => ac.User)
                .WithMany(u => u.AchivmentCounter)
                .HasForeignKey(ac => ac.UserId);

            modelBuilder.Entity<AchievementCounter>()
                .HasOne(ac => ac.Achievment)
                .WithMany()
                .HasForeignKey(ac => ac.AchievmentId);

            modelBuilder.Entity<Exercises>()
            .HasMany(e => e.MusclesEngaded)
            .WithMany()
            .UsingEntity(j => j.ToTable("ExerciseMuscles"));

            var layoutSettingsConverter = new ValueConverter<LayoutSettings, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<LayoutSettings>(v, (JsonSerializerOptions)null)
            );

            modelBuilder.Entity<AppUser>()
                .Property(u => u.LayoutSettings)
                .HasConversion(layoutSettingsConverter)
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<Challange>()
              .HasOne(c => c.Creator)
              .WithMany(c => c.Challenges)
              .HasForeignKey(c => c.CreatorId)
              .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AppUser>()
                .HasMany(u => u.ActiveChallanges)
                .WithOne()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            modelBuilder.Entity<ActiveChallange>()
                .HasOne(ac => ac.Challenge)
                .WithMany()
                .HasForeignKey(ac => ac.ChallengeId)
                .IsRequired();


            modelBuilder.Entity<UserBadges>()
                .HasOne(ub => ub.User)
                .WithMany(u => u.UserBadges)
                .HasForeignKey(ub => ub.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserBadges>()
                .HasOne(ub => ub.Challange)
                .WithMany()
                .HasForeignKey(ub => ub.ChallangeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserFollower>()
                .HasKey(uf => new { uf.FollowerId, uf.FolloweeId });

            modelBuilder.Entity<UserFollower>()
               .HasOne(uf => uf.Follower)
               .WithMany(u => u.Following)
               .HasForeignKey(uf => uf.FollowerId)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserFollower>()
                .HasOne(uf => uf.Followee)
                .WithMany(u => u.Followers)
                .HasForeignKey(uf => uf.FolloweeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserBlock>()
                .HasKey(ub => new { ub.BlockerId, ub.BlockedId });

            modelBuilder.Entity<UserBlock>()
                .HasOne(ub => ub.Blocker)
                .WithMany(u => u.BlockedUsers)
                .HasForeignKey(ub => ub.BlockerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserBlock>()
                .HasOne(ub => ub.Blocked)
                .WithMany(u => u.BlockedByUsers)
                .HasForeignKey(ub => ub.BlockedId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }

        public DbSet<AppUser> AppUser { get; set; }
        public DbSet<UserInformation> UserInformation { get; set; }
        public DbSet<CalorieInformation> CalorieInformation { get; set; }
        public DbSet<ReportedIngredients> ReportedIngredients { get; set; }
        public DbSet<AddProductRequest> AddProductRequest { get; set; }
        public DbSet<ReportedMeals> ReportedMeals { get; set; }
        public DbSet<Achievment> Achievment { get; set; }
        public DbSet<AchievementCounter> AchievementCounters { get; set; }
        public DbSet<Exercises> Exercises { get; set; }
        public DbSet<Muscle> Muscles { get; set; }
        public DbSet<Challange> Challanges { get; set; }
        public DbSet<Creator> Creators { get; set; }
        public DbSet<ActiveChallange> ActiveChallange { get; set; }
        public DbSet<UserBadges> UserBadges { get; set; }
        public DbSet<UserFollower> UserFollower { get; set; }
        public DbSet<UserBlock> UserBlock { get; set; }
        public DbSet<Models.Requests.ReportedUsers> ReportedUsers { get; set; }
    }
}
