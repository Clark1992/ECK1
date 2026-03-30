CREATE TABLE [dbo].[EntityState]
(
    [EntityId] UNIQUEIDENTIFIER NOT NULL,
    [EntityType] NVARCHAR(128) NOT NULL,
    [ExpectedVersion] INT NOT NULL,
    [LastEventOccuredAt] DATETIME2 NOT NULL,
    [ReconciledAt] DATETIME2 NULL,

    CONSTRAINT [PK_EntityState] PRIMARY KEY CLUSTERED ([EntityId], [EntityType])
);

CREATE NONCLUSTERED INDEX [IX_EntityState_ReconciledAt]
ON [dbo].[EntityState] ([ReconciledAt] ASC);

CREATE TABLE [dbo].[ReconcileFailures]
(
    [Id] INT IDENTITY(1,1) NOT NULL,
    [EntityId] UNIQUEIDENTIFIER NOT NULL,
    [EntityType] NVARCHAR(128) NOT NULL,
    [FailedPlugin] NVARCHAR(64) NOT NULL,
    [IsFullHistoryRebuild] BIT NOT NULL,
    [FailedAt] DATETIME2 NOT NULL,
    [DispatchedAt] DATETIME2 NULL,

    CONSTRAINT [PK_ReconcileFailures] PRIMARY KEY CLUSTERED ([Id])
);

CREATE NONCLUSTERED INDEX [IX_ReconcileFailures_DispatchedAt]
ON [dbo].[ReconcileFailures] ([DispatchedAt] ASC);

CREATE NONCLUSTERED INDEX [IX_ReconcileFailures_Entity]
ON [dbo].[ReconcileFailures] ([EntityId], [EntityType], [FailedPlugin]);
