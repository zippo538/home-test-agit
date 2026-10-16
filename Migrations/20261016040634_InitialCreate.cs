using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace home_test_agit.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PolicyVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ManagerId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsAuditor = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Users_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SystemOwnerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_Users_SystemOwnerId",
                        column: x => x.SystemOwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccessRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequesterId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AccessLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Justification = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    IsHighRisk = table.Column<bool>(type: "INTEGER", nullable: false),
                    PolicyVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    RejectReason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRequests", x => x.Id);
                    table.CheckConstraint("CK_AccessRequests_AccessLevel", "AccessLevel IN ('Read','Admin')");
                    table.CheckConstraint("CK_AccessRequests_Environment", "Environment IN ('NonProduction','Production')");
                    table.CheckConstraint("CK_AccessRequests_Justification", "length(trim(Justification)) > 0");
                    table.CheckConstraint("CK_AccessRequests_RejectReason", "Status <> 'Rejected' OR length(trim(coalesce(RejectReason, ''))) > 0");
                    table.CheckConstraint("CK_AccessRequests_Status", "Status IN ('PendingManager','PendingSystemOwner','Approved','Rejected')");
                    table.CheckConstraint("CK_AccessRequests_Version", "Version >= 1");
                    table.ForeignKey(
                        name: "FK_AccessRequests_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessRequests_PolicyVersions_PolicyVersionId",
                        column: x => x.PolicyVersionId,
                        principalTable: "PolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessRequests_Users_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    FromStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    RequestVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    PolicyVersionCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.CheckConstraint("CK_AuditEvents_Action", "Action IN ('Created','ManagerApproved','SystemOwnerApproved','Rejected')");
                    table.ForeignKey(
                        name: "FK_AuditEvents_AccessRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "AccessRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "PolicyVersions",
                columns: new[] { "Id", "Code", "IsActive" },
                values: new object[] { 1, "v1", true });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "DisplayName", "Email", "IsAuditor", "ManagerId" },
                values: new object[,]
                {
                    { 2, "Bob", "bob@example.local", false, null },
                    { 3, "Carol", "carol@example.local", false, null },
                    { 4, "Dana", "dana@example.local", false, null },
                    { 5, "Erin", "erin@example.local", true, null }
                });

            migrationBuilder.InsertData(
                table: "Applications",
                columns: new[] { "Id", "Name", "SystemOwnerId" },
                values: new object[,]
                {
                    { 1, "CRM", 3 },
                    { 2, "Finance Portal", 4 }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "DisplayName", "Email", "IsAuditor", "ManagerId" },
                values: new object[] { 1, "Alice", "alice@example.local", false, 2 });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_ApplicationId",
                table: "AccessRequests",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_PolicyVersionId",
                table: "AccessRequests",
                column: "PolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_RequesterId",
                table: "AccessRequests",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_RequesterId_ClientRequestId",
                table: "AccessRequests",
                columns: new[] { "RequesterId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_Status_ApplicationId",
                table: "AccessRequests",
                columns: new[] { "Status", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Name",
                table: "Applications",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_SystemOwnerId",
                table: "Applications",
                column: "SystemOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId",
                table: "AuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_RequestId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "RequestId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyVersions_Code",
                table: "PolicyVersions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerId",
                table: "Users",
                column: "ManagerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "AccessRequests");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "PolicyVersions");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
