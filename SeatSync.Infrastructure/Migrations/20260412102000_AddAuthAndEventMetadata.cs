using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SeatSync.Infrastructure.Data;

#nullable disable

namespace SeatSync.Infrastructure.Migrations;

[DbContext(typeof(SeatSyncDbContext))]
[Migration("20260412102000_AddAuthAndEventMetadata")]
public partial class AddAuthAndEventMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.Events', 'Agenda') IS NULL
            BEGIN
                ALTER TABLE dbo.Events
                ADD Agenda NVARCHAR(2000) NULL;
            END
            """);

        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.Events', 'CreatedByUserId') IS NULL
            BEGIN
                ALTER TABLE dbo.Events
                ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AppUsers
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AppUsers PRIMARY KEY,
                    Email NVARCHAR(200) NOT NULL,
                    DisplayName NVARCHAR(200) NOT NULL,
                    Password NVARCHAR(200) NOT NULL,
                    Role INT NOT NULL,
                    CreatedAt DATETIMEOFFSET(7) NOT NULL
                );
            END
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_AppUsers_Email'
                  AND object_id = OBJECT_ID(N'dbo.AppUsers')
            )
            BEGIN
                CREATE UNIQUE INDEX IX_AppUsers_Email
                ON dbo.AppUsers (Email);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS IX_AppUsers_Email ON dbo.AppUsers;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS dbo.AppUsers;");

        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.Events', 'CreatedByUserId') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.Events DROP COLUMN CreatedByUserId;
            END
            """);

        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.Events', 'Agenda') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.Events DROP COLUMN Agenda;
            END
            """);
    }
}
