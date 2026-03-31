SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID(N'[dbo].[EventFailures]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[EventFailures]
        (
            [EntityType] NVARCHAR(64) NOT NULL,
            [EntityId] UNIQUEIDENTIFIER NOT NULL,
            [FailedEventType] NVARCHAR(512) NOT NULL,
            [FailureOccurredAt] DATETIMEOFFSET NOT NULL,
            [ErrorMessage] NVARCHAR(4000) NULL,
            [StackTrace] NVARCHAR(4000) NULL,

            CONSTRAINT [PK_EventFailures] PRIMARY KEY CLUSTERED ([EntityType] ASC, [EntityId] ASC)
        );

        CREATE NONCLUSTERED INDEX [IX_EventFailures_FailureOccurredAt]
        ON [dbo].[EventFailures] ([FailureOccurredAt] ASC);
    END

    IF OBJECT_ID(N'[dbo].[SampleEventFailures]', N'U') IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[EventFailures] ([EntityType], [EntityId], [FailedEventType], [FailureOccurredAt], [ErrorMessage], [StackTrace])
        SELECT
            N'Sample' AS [EntityType],
            [SampleId] AS [EntityId],
            [FailedEventType],
            [FailureOccurredAt],
            [ErrorMessage],
            [StackTrace]
        FROM [dbo].[SampleEventFailures];

        DROP TABLE [dbo].[SampleEventFailures];
    END

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;

    THROW;
END CATCH;
