﻿using ElGato_API.Migrations;
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

            modelBuilder.Entity<AchievmentCounters>()
                .HasOne(ac => ac.User)
                .WithMany(u => u.AchivmentCounter)
                .HasForeignKey(ac => ac.UserId);

            modelBuilder.Entity<AchievmentCounters>()
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
                .HasForeignKey("AppUserId")
                .IsRequired();

            modelBuilder.Entity<ActiveChallange>()
                .HasOne(ac => ac.Challenge)
                .WithMany()
                .HasForeignKey(ac => ac.ChallengeId)
                .IsRequired();

            base.OnModelCreating(modelBuilder);
        }

        public DbSet<AppUser> AppUser { get; set; }
        public DbSet<UserInformation> UserInformation { get; set; }
        public DbSet<CalorieInformation> CalorieInformation { get; set; }
        public DbSet<ReportedIngredients> ReportedIngredients { get; set; }
        public DbSet<AddProductRequest> AddProductRequest { get; set; }
        public DbSet<ReportedMeals> ReportedMeals { get; set; }
        public DbSet<Achievment> Achievment { get; set; }
        public DbSet<AchievmentCounters> AchievmentCounters { get; set; }
        public DbSet<Exercises> Exercises { get; set; }
        public DbSet<Muscle> Muscles { get; set; }
        public DbSet<Challange> Challanges { get; set; }
        public DbSet<Creator> Creators { get; set; }
        public DbSet<ActiveChallange> ActiveChallange { get; set; }
    }
}
