using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClassroomsAndRepositoryPushActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PushedAt",
                schema: "portfolio",
                table: "RepositoryAnalysis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Classrooms",
                schema: "portfolio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classrooms", x => x.Id);
                    table.UniqueConstraint("AK_Classrooms_Id_ApplicationUserId", x => new { x.Id, x.ApplicationUserId });
                    table.CheckConstraint("CK_Classrooms_Name", "char_length(btrim(\"Name\")) BETWEEN 2 AND 100");
                    table.ForeignKey(
                        name: "FK_Classrooms_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalSchema: "portfolio",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassroomMembers",
                schema: "portfolio",
                columns: table => new
                {
                    ClassroomId = table.Column<Guid>(type: "uuid", nullable: false),
                    GitHubProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassroomMembers", x => new { x.ClassroomId, x.GitHubProfileId });
                    table.ForeignKey(
                        name: "FK_ClassroomMembers_Classrooms_ClassroomId_ApplicationUserId",
                        columns: x => new { x.ClassroomId, x.ApplicationUserId },
                        principalSchema: "portfolio",
                        principalTable: "Classrooms",
                        principalColumns: new[] { "Id", "ApplicationUserId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassroomMembers_UserSavedProfiles",
                        columns: x => new { x.ApplicationUserId, x.GitHubProfileId },
                        principalSchema: "portfolio",
                        principalTable: "UserSavedProfiles",
                        principalColumns: new[] { "UserId", "GitHubProfileId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomMembers_ApplicationUserId_GitHubProfileId",
                schema: "portfolio",
                table: "ClassroomMembers",
                columns: new[] { "ApplicationUserId", "GitHubProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomMembers_ClassroomId_ApplicationUserId",
                schema: "portfolio",
                table: "ClassroomMembers",
                columns: new[] { "ClassroomId", "ApplicationUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_ApplicationUserId_UpdatedAt",
                schema: "portfolio",
                table: "Classrooms",
                columns: new[] { "ApplicationUserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassroomMembers",
                schema: "portfolio");

            migrationBuilder.DropTable(
                name: "Classrooms",
                schema: "portfolio");

            migrationBuilder.DropColumn(
                name: "PushedAt",
                schema: "portfolio",
                table: "RepositoryAnalysis");
        }
    }
}
