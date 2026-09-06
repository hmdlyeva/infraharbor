using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraHarbor.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(InfraHarborDbContext))]
    [Migration("20260906120500_AddBrandingSettings")]
    public partial class AddBrandingSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrandingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FaviconUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SupportUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DocumentationUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FooterText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LoginHeadline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandingSettings", x => x.Id);
                    table.CheckConstraint(
                        "CK_BrandingSettings_InstallationSingleton",
                        "\"Id\" = '00000000-0000-0000-0000-000000000019'::uuid");
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BrandingSettings");
        }
    }
}
