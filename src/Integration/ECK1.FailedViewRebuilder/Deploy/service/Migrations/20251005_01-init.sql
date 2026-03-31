CREATE TABLE [dbo].[SampleEventFailures]
(
    [SampleId] UNIQUEIDENTIFIER NOT NULL,
    [FailedEventType] NVARCHAR(512) NOT NULL,
    [FailureOccurredAt] DATETIMEOFFSET NOT NULL,
    [ErrorMessage] NVARCHAR(4000) NULL,
    [StackTrace] NVARCHAR(4000) NULL,
    
    CONSTRAINT [PK_SampleEventFailure] PRIMARY KEY CLUSTERED ([SampleId] ASC)
);

CREATE NONCLUSTERED INDEX IX_SampleEventFailures_FailureOccurredAt
ON [dbo].[SampleEventFailures] ([FailureOccurredAt] ASC);
