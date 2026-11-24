using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OOAD.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Gym_Rules");

            migrationBuilder.DropTable(
                name: "Nutrition_Plans");

            migrationBuilder.DropTable(
                name: "System_Settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Gym_Rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Branch_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Created_By = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Is_Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gym_Rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gym_Rules_Branches_Branch_Id",
                        column: x => x.Branch_Id,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Gym_Rules_Users_Created_By",
                        column: x => x.Created_By,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Nutrition_Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Measurement_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Member_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Activity_Level = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Carb_G = table.Column<int>(type: "int", nullable: false),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Daily_Calories = table.Column<int>(type: "int", nullable: false),
                    Dietary_Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Fat_G = table.Column<int>(type: "int", nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Meal_Suggestion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Protein_G = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nutrition_Plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nutrition_Plans_Body_Measurements_Measurement_Id",
                        column: x => x.Measurement_Id,
                        principalTable: "Body_Measurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Nutrition_Plans_Members_Member_Id",
                        column: x => x.Member_Id,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "System_Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Branch_Id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Updated_By = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Data_Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Setting_Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Setting_Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_System_Settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_System_Settings_Branches_Branch_Id",
                        column: x => x.Branch_Id,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_System_Settings_Users_Updated_By",
                        column: x => x.Updated_By,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gym_Rules_Branch_Id",
                table: "Gym_Rules",
                column: "Branch_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Gym_Rules_Created_By",
                table: "Gym_Rules",
                column: "Created_By");

            migrationBuilder.CreateIndex(
                name: "IX_Nutrition_Plans_Measurement_Id",
                table: "Nutrition_Plans",
                column: "Measurement_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Nutrition_Plans_Member_Id",
                table: "Nutrition_Plans",
                column: "Member_Id");

            migrationBuilder.CreateIndex(
                name: "IX_System_Settings_Branch_Id",
                table: "System_Settings",
                column: "Branch_Id");

            migrationBuilder.CreateIndex(
                name: "IX_System_Settings_Updated_By",
                table: "System_Settings",
                column: "Updated_By");
        }
    }
}
