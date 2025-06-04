using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElGato_API.Migrations
{
    /// <inheritdoc />
    public partial class UserFollowerRequestt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserFollowerRequest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequesterId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsPending = table.Column<bool>(type: "bit", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFollowerRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFollowerRequest_AspNetUsers_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserFollowerRequest_AspNetUsers_TargetId",
                        column: x => x.TargetId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFollowerRequest_RequesterId_TargetId",
                table: "UserFollowerRequest",
                columns: new[] { "RequesterId", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFollowerRequest_TargetId",
                table: "UserFollowerRequest",
                column: "TargetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFollowerRequest");
        }
    }
}
