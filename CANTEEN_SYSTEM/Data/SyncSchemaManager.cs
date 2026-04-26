using Microsoft.EntityFrameworkCore;

namespace CANTEEN_SYSTEM.Data;

public static class SyncSchemaManager
{
    public static async Task EnsureAsync(CanteenDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (db.Database.IsSqlite())
        {
            await EnsureSqliteSchemaAsync(db);
            return;
        }

        if (db.Database.IsSqlServer())
        {
            await EnsureSqlServerSchemaAsync(db);
        }
    }

    private static async Task EnsureSqliteSchemaAsync(CanteenDbContext db)
    {
        var commands = new[]
        {
            "ALTER TABLE Products ADD COLUMN SyncId TEXT NULL;",
            "ALTER TABLE Products ADD COLUMN LastModifiedAt TEXT NULL;",
            "ALTER TABLE Employees ADD COLUMN SyncId TEXT NULL;",
            "ALTER TABLE Employees ADD COLUMN LastModifiedAt TEXT NULL;",
            "ALTER TABLE Orders ADD COLUMN SyncId TEXT NULL;",
            "ALTER TABLE Orders ADD COLUMN LastModifiedAt TEXT NULL;",
            "ALTER TABLE OrderItems ADD COLUMN SyncId TEXT NULL;",
            "ALTER TABLE OrderItems ADD COLUMN LastModifiedAt TEXT NULL;",
            """
            CREATE TABLE IF NOT EXISTS SyncQueue (
                Id INTEGER NOT NULL CONSTRAINT PK_SyncQueue PRIMARY KEY AUTOINCREMENT,
                EntityType TEXT NOT NULL,
                EntitySyncId TEXT NOT NULL,
                Operation TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                LastAttemptAt TEXT NULL,
                LastError TEXT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AppState (
                Key TEXT NOT NULL CONSTRAINT PK_AppState PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Products_SyncId ON Products (SyncId);",
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Employees_SyncId ON Employees (SyncId);",
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Orders_SyncId ON Orders (SyncId);",
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_OrderItems_SyncId ON OrderItems (SyncId);",
            "CREATE INDEX IF NOT EXISTS IX_SyncQueue_EntityType_EntitySyncId_Operation ON SyncQueue (EntityType, EntitySyncId, Operation);"
        };

        foreach (var command in commands)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(command);
            }
            catch (Exception ex) when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
            {
                // Existing local databases may already have been upgraded.
            }
        }
    }

    private static async Task EnsureSqlServerSchemaAsync(CanteenDbContext db)
    {
        var commands = new[]
        {
            """
            IF COL_LENGTH('Products', 'SyncId') IS NULL
                ALTER TABLE Products ADD SyncId nvarchar(32) NULL;
            """,
            """
            IF COL_LENGTH('Products', 'LastModifiedAt') IS NULL
                ALTER TABLE Products ADD LastModifiedAt datetime2 NULL;
            """,
            """
            IF COL_LENGTH('Employees', 'SyncId') IS NULL
                ALTER TABLE Employees ADD SyncId nvarchar(32) NULL;
            """,
            """
            IF COL_LENGTH('Employees', 'LastModifiedAt') IS NULL
                ALTER TABLE Employees ADD LastModifiedAt datetime2 NULL;
            """,
            """
            IF COL_LENGTH('Orders', 'SyncId') IS NULL
                ALTER TABLE Orders ADD SyncId nvarchar(32) NULL;
            """,
            """
            IF COL_LENGTH('Orders', 'LastModifiedAt') IS NULL
                ALTER TABLE Orders ADD LastModifiedAt datetime2 NULL;
            """,
            """
            IF COL_LENGTH('OrderItems', 'SyncId') IS NULL
                ALTER TABLE OrderItems ADD SyncId nvarchar(32) NULL;
            """,
            """
            IF COL_LENGTH('OrderItems', 'LastModifiedAt') IS NULL
                ALTER TABLE OrderItems ADD LastModifiedAt datetime2 NULL;
            """,
            """
            IF OBJECT_ID(N'SyncQueue', N'U') IS NULL
            BEGIN
                CREATE TABLE SyncQueue (
                    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SyncQueue PRIMARY KEY,
                    EntityType nvarchar(40) NOT NULL,
                    EntitySyncId nvarchar(32) NOT NULL,
                    Operation nvarchar(20) NOT NULL,
                    CreatedAt datetime2 NOT NULL,
                    LastAttemptAt datetime2 NULL,
                    LastError nvarchar(2000) NULL
                );
            END
            """,
            """
            IF OBJECT_ID(N'AppState', N'U') IS NULL
            BEGIN
                CREATE TABLE AppState (
                    [Key] nvarchar(120) NOT NULL CONSTRAINT PK_AppState PRIMARY KEY,
                    [Value] nvarchar(4000) NOT NULL
                );
            END
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_SyncId' AND object_id = OBJECT_ID('Products'))
                CREATE UNIQUE INDEX IX_Products_SyncId ON Products (SyncId) WHERE SyncId IS NOT NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_SyncId' AND object_id = OBJECT_ID('Employees'))
                CREATE UNIQUE INDEX IX_Employees_SyncId ON Employees (SyncId) WHERE SyncId IS NOT NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_SyncId' AND object_id = OBJECT_ID('Orders'))
                CREATE UNIQUE INDEX IX_Orders_SyncId ON Orders (SyncId) WHERE SyncId IS NOT NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrderItems_SyncId' AND object_id = OBJECT_ID('OrderItems'))
                CREATE UNIQUE INDEX IX_OrderItems_SyncId ON OrderItems (SyncId) WHERE SyncId IS NOT NULL;
            """,
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SyncQueue_EntityType_EntitySyncId_Operation' AND object_id = OBJECT_ID('SyncQueue'))
                CREATE INDEX IX_SyncQueue_EntityType_EntitySyncId_Operation ON SyncQueue (EntityType, EntitySyncId, Operation);
            """
        };

        foreach (var command in commands)
        {
            await db.Database.ExecuteSqlRawAsync(command);
        }
    }
}
