IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Username] nvarchar(50) NOT NULL,
        [Email] nvarchar(150) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(255) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE TABLE [ActiveSessions] (
        [UserId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(255) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ActiveSessions] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_ActiveSessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE TABLE [Subjects] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [CreatedBy] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Subjects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Subjects_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE TABLE [Exams] (
        [Id] int NOT NULL IDENTITY,
        [SubjectId] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [DurationMinutes] int NOT NULL,
        [GenerateMode] nvarchar(20) NOT NULL,
        [QuestionCount] int NULL,
        [TotalPoints] decimal(6,2) NULL,
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedBy] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Exams] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Exams_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Exams_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE TABLE [Questions] (
        [Id] int NOT NULL IDENTITY,
        [SubjectId] int NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [Difficulty] nvarchar(10) NOT NULL,
        [Points] decimal(4,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedBy] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Questions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Questions_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Questions_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE TABLE [ExamAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [ExamId] int NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [SubmittedAt] datetime2 NULL,
        [Score] decimal(5,2) NULL,
        [TotalPoints] decimal(5,2) NULL,
        [IpAddress] nvarchar(50) NULL,
        [Status] nvarchar(20) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        CONSTRAINT [PK_ExamAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExamAttempts_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExamAttempts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE TABLE [Answers] (
        [Id] int NOT NULL IDENTITY,
        [QuestionId] int NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [IsCorrect] bit NOT NULL,
        [OrderIndex] tinyint NOT NULL,
        CONSTRAINT [PK_Answers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Answers_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE TABLE [ExamAttemptAnswers] (
        [Id] int NOT NULL IDENTITY,
        [AttemptId] uniqueidentifier NOT NULL,
        [QuestionId] int NOT NULL,
        [AnswerIds] nvarchar(max) NULL,
        [TextInput] nvarchar(500) NULL,
        [IsCorrect] bit NULL,
        [PointsEarned] decimal(4,2) NULL,
        CONSTRAINT [PK_ExamAttemptAnswers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExamAttemptAnswers_ExamAttempts_AttemptId] FOREIGN KEY ([AttemptId]) REFERENCES [ExamAttempts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ExamAttemptAnswers_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE TABLE [ExamQuestionSnapshots] (
        [AttemptId] uniqueidentifier NOT NULL,
        [QuestionId] int NOT NULL,
        [OrderIndex] tinyint NOT NULL,
        CONSTRAINT [PK_ExamQuestionSnapshots] PRIMARY KEY ([AttemptId], [QuestionId]),
        CONSTRAINT [FK_ExamQuestionSnapshots_ExamAttempts_AttemptId] FOREIGN KEY ([AttemptId]) REFERENCES [ExamAttempts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ExamQuestionSnapshots_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Answers_QuestionId] ON [Answers] ([QuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExamAttemptAnswers_AttemptId_QuestionId] ON [ExamAttemptAnswers] ([AttemptId], [QuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ExamAttemptAnswers_QuestionId] ON [ExamAttemptAnswers] ([QuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExamAttempts_ExamId_UserId] ON [ExamAttempts] ([ExamId], [UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ExamAttempts_UserId] ON [ExamAttempts] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ExamQuestionSnapshots_QuestionId] ON [ExamQuestionSnapshots] ([QuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Exams_CreatedBy] ON [Exams] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Exams_SubjectId] ON [Exams] ([SubjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Questions_CreatedBy] ON [Questions] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Questions_SubjectId] ON [Questions] ([SubjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Subjects_CreatedBy] ON [Subjects] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602031219_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260602031219_InitialCreate', N'8.0.23');
END;
GO

COMMIT;
GO

