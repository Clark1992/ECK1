BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Sample2Events', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[Sample2Events]
        (
            [EventId] UNIQUEIDENTIFIER NOT NULL 
                CONSTRAINT [DF_Sample2Events_EventId] DEFAULT NEWID(),

            [Sample2Id] UNIQUEIDENTIFIER NOT NULL,

            [EventType] NVARCHAR(256) NOT NULL,

            [EventData] NVARCHAR(MAX) NOT NULL,

            [OccurredAt] datetimeoffset NOT NULL 
                CONSTRAINT [DF_Sample2Events_OccurredAt] DEFAULT (SYSDATETIMEOFFSET()),

            [Version] INT NOT NULL 
                CONSTRAINT [CK_Sample2Events_Version_Positive] CHECK ([Version] > 0),

            CONSTRAINT [PK_Sample2Events] PRIMARY KEY ([EventId])
        );

        CREATE UNIQUE INDEX [IX_Sample2Events_Sample2Id_Version]
            ON [dbo].[Sample2Events] ([Sample2Id], [Version]);
    END

    IF OBJECT_ID(N'dbo.Sample2Snapshots', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[Sample2Snapshots] (
            [SnapshotId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [Sample2Id] UNIQUEIDENTIFIER NOT NULL,
            [Version] INT NOT NULL,
            [SnapshotData] NVARCHAR(MAX) NOT NULL,
            [CreatedAt] datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            CONSTRAINT UQ_Sample2Snapshots_Sample2Id_Version UNIQUE ([Sample2Id], [Version])
        );

        CREATE INDEX IX_Sample2Snapshots_Sample2Id_Version
            ON [dbo].[Sample2Snapshots] ([Sample2Id], [Version] DESC);
    END

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();

    RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH
