BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Samples', N'U') IS NOT NULL
        DROP TABLE dbo.Samples


    IF OBJECT_ID(N'dbo.SampleEvents', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[SampleEvents]
        (
            [EventId] UNIQUEIDENTIFIER NOT NULL 
                CONSTRAINT [DF_SampleEvents_EventId] DEFAULT NEWID(),

            [SampleId] UNIQUEIDENTIFIER NOT NULL,

            [EventType] NVARCHAR(256) NOT NULL,

            [EventData] NVARCHAR(MAX) NOT NULL,

            [OccurredAt] datetimeoffset NOT NULL 
                CONSTRAINT [DF_SampleEvents_OccurredAt] DEFAULT (SYSDATETIMEOFFSET()),

            [Version] INT NOT NULL 
                CONSTRAINT [CK_SampleEvents_Version_Positive] CHECK ([Version] > 0),

            CONSTRAINT [PK_SampleEvents] PRIMARY KEY ([EventId])
        );

        CREATE UNIQUE INDEX [IX_SampleEvents_SampleId_Version]
        ON [dbo].[SampleEvents] ([SampleId], [Version]);
    END

    IF OBJECT_ID(N'dbo.SampleSnapshots', N'U') IS NULL
    BEGIN
    CREATE TABLE [dbo].[SampleSnapshots] (
        [SnapshotId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [SampleId] UNIQUEIDENTIFIER NOT NULL,
        [Version] INT NOT NULL,
        [SnapshotData] NVARCHAR(MAX) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT UQ_SampleSnapshots_SampleId_Version UNIQUE ([SampleId], [Version])
    );

    CREATE INDEX IX_SampleSnapshots_SampleId_Version
        ON [dbo].[SampleSnapshots] ([SampleId], [Version] DESC);
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
END CATCH;
