using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Portfolio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubOAuthConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                schema: "portfolio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitHubConnections",
                schema: "portfolio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: false),
                    GitHubUserId = table.Column<long>(type: "bigint", nullable: false),
                    GitHubUsername = table.Column<string>(type: "character varying(39)", maxLength: 39, nullable: false),
                    AvatarUrl = table.Column<string>(type: "text", nullable: false),
                    AccessTokenEncrypted = table.Column<string>(type: "text", nullable: false),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InvalidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubConnections_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalSchema: "portfolio",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GitHubOAuthAttempts",
                schema: "portfolio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: false),
                    StateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VerifierEncrypted = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubOAuthAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubOAuthAttempts_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalSchema: "portfolio",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitHubConnections_ApplicationUserId",
                schema: "portfolio",
                table: "GitHubConnections",
                column: "ApplicationUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubConnections_GitHubUserId",
                schema: "portfolio",
                table: "GitHubConnections",
                column: "GitHubUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubOAuthAttempts_ApplicationUserId",
                schema: "portfolio",
                table: "GitHubOAuthAttempts",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubOAuthAttempts_ExpiresAt",
                schema: "portfolio",
                table: "GitHubOAuthAttempts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubOAuthAttempts_StateHash",
                schema: "portfolio",
                table: "GitHubOAuthAttempts",
                column: "StateHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys",
                schema: "portfolio");

            migrationBuilder.DropTable(
                name: "GitHubConnections",
                schema: "portfolio");

            migrationBuilder.DropTable(
                name: "GitHubOAuthAttempts",
                schema: "portfolio");
        }
    }
}
