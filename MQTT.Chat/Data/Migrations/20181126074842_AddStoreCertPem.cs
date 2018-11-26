using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MQTT.Chat.Data.Migrations
{
    public partial class AddStoreCertPem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetainedMessages",
                columns: table => new
                {
                    Topic = table.Column<string>(nullable: true),
                    Payload = table.Column<byte[]>(nullable: true),
                    QualityOfServiceLevel = table.Column<int>(nullable: false),
                    Retain = table.Column<bool>(nullable: false),
                    Id = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetainedMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreCertPem",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    ClientCert = table.Column<string>(nullable: true),
                    ClientKey = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreCertPem", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetainedMessages");

            migrationBuilder.DropTable(
                name: "StoreCertPem");
        }
    }
}
