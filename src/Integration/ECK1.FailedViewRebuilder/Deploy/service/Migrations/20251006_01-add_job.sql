CREATE TABLE [dbo].[JobHistory]
(
    [Id] int NOT NULL IDENTITY(1, 1),
    [Name] NVARCHAR(512) NOT NULL,
    [StartedAt] DATETIMEOFFSET NOT NULL,
    [FinishedAt] DATETIMEOFFSET NULL,
    [IsSuccess] BIT NULL,
    [ErrorMessage] NVARCHAR(4000) NULL,
    
    CONSTRAINT [PK_JobHistory] PRIMARY KEY CLUSTERED ([Id] ASC)
);

CREATE NONCLUSTERED INDEX IX_JobHistory_FailureOccurredAt
ON [dbo].[JobHistory] ([StartedAt] ASC);
