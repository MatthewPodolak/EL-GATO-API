using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElGato_API.Migrations
{
    /// <inheritdoc />
    public partial class fixedAchievmentCountersTypos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActiveChallange_AspNetUsers_AppUserId",
                table: "ActiveChallange");

            migrationBuilder.DropTable(
                name: "AchievmentCounters");

            migrationBuilder.RenameColumn(
                name: "AppUserId",
                table: "ActiveChallange",
                newName: "UserId1");

            migrationBuilder.RenameIndex(
                name: "IX_ActiveChallange_AppUserId",
                table: "ActiveChallange",
                newName: "IX_ActiveChallange_UserId1");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ActiveChallange",
                type: "nvarchar(450)",
                nullable: true,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AchievementCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LastCount = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Counter = table.Column<int>(type: "int", nullable: false),
                    AchievmentId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementCounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AchievementCounters_Achievment_AchievmentId",
                        column: x => x.AchievmentId,
                        principalTable: "Achievment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AchievementCounters_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveChallange_UserId",
                table: "ActiveChallange",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AchievementCounters_AchievmentId",
                table: "AchievementCounters",
                column: "AchievmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AchievementCounters_UserId",
                table: "AchievementCounters",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActiveChallange_AspNetUsers_UserId",
                table: "ActiveChallange",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActiveChallange_AspNetUsers_UserId",
                table: "ActiveChallange");

            migrationBuilder.DropForeignKey(
                name: "FK_ActiveChallange_AspNetUsers_UserId1",
                table: "ActiveChallange");

            migrationBuilder.DropTable(
                name: "AchievementCounters");

            migrationBuilder.DropIndex(
                name: "IX_ActiveChallange_UserId",
                table: "ActiveChallange");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ActiveChallange");

            migrationBuilder.RenameColumn(
                name: "UserId1",
                table: "ActiveChallange",
                newName: "AppUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ActiveChallange_UserId1",
                table: "ActiveChallange",
                newName: "IX_ActiveChallange_AppUserId");

            migrationBuilder.CreateTable(
                name: "AchievmentCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AchievmentId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Counter = table.Column<int>(type: "int", nullable: false),
                    LastCount = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievmentCounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AchievmentCounters_Achievment_AchievmentId",
                        column: x => x.AchievmentId,
                        principalTable: "Achievment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AchievmentCounters_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AchievmentCounters_AchievmentId",
                table: "AchievmentCounters",
                column: "AchievmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AchievmentCounters_UserId",
                table: "AchievmentCounters",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActiveChallange_AspNetUsers_AppUserId",
                table: "ActiveChallange",
                column: "AppUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
