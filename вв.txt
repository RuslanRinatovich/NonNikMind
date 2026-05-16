-- ============================================
-- Скрипт создания и наполнения базы данных 
-- для дипломного проекта "MindKeeper"
-- (Автор: по материалам ТЗ)
-- ============================================

-- 1. Удаляем старую базу, если она существует (осторожно!)
USE master;
GO
IF DB_ID('PersonalKnowledgeDB') IS NOT NULL
    DROP DATABASE PersonalKnowledgeDB;
GO

-- 2. Создаём базу данных
CREATE DATABASE PersonalKnowledgeDB;
GO

USE PersonalKnowledgeDB;
GO

-- 3. Создание таблиц (структура полностью сохранена, только убраны лишние ограничения)
CREATE TABLE [dbo].[Users](
    [UserID] [int] IDENTITY(1,1) NOT NULL,
    [Username] [nvarchar](100) NOT NULL,
    [PasswordHash] [nvarchar](255) NOT NULL,
    [FullName] [nvarchar](200) NULL,
    [CreatedAt] [datetime2](7) NULL,
    [Role] [nvarchar](20) NOT NULL DEFAULT 'User',
    [IsLocked] [bit] NOT NULL DEFAULT 0,
 CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserID] ASC),
 CONSTRAINT [UQ_Users_Username] UNIQUE NONCLUSTERED ([Username] ASC)
);

CREATE TABLE [dbo].[Notes](
    [NoteID] [int] IDENTITY(1,1) NOT NULL,
    [UserID] [int] NOT NULL,
    [Title] [nvarchar](400) NOT NULL,
    [Content] [nvarchar](max) NULL,
    [ParentNoteID] [int] NULL,
    [CreatedAt] [datetime2](7) NOT NULL DEFAULT GETDATE(),
    [UpdatedAt] [datetime2](7) NOT NULL DEFAULT GETDATE(),
    [IsDeleted] [bit] NOT NULL DEFAULT 0,
    [ReminderDate] [datetime2](7) NULL,
    [IsReminderCompleted] [bit] NOT NULL DEFAULT 0,
    [ReminderNote] [nvarchar](500) NULL,
 CONSTRAINT [PK_Notes] PRIMARY KEY CLUSTERED ([NoteID] ASC),
 CONSTRAINT [UQ_User_Title] UNIQUE NONCLUSTERED ([UserID] ASC, [Title] ASC),
 CONSTRAINT [FK_Notes_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID]) ON DELETE CASCADE,
 CONSTRAINT [FK_Notes_Parent] FOREIGN KEY ([ParentNoteID]) REFERENCES [dbo].[Notes] ([NoteID])
);

CREATE TABLE [dbo].[Tags](
    [TagID] [int] IDENTITY(1,1) NOT NULL,
    [TagName] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_Tags] PRIMARY KEY CLUSTERED ([TagID] ASC),
 CONSTRAINT [UQ_Tags_TagName] UNIQUE NONCLUSTERED ([TagName] ASC)
);

CREATE TABLE [dbo].[NoteTags](
    [NoteID] [int] NOT NULL,
    [TagID] [int] NOT NULL,
 CONSTRAINT [PK_NoteTags] PRIMARY KEY CLUSTERED ([NoteID] ASC, [TagID] ASC),
 CONSTRAINT [FK_NoteTags_Note] FOREIGN KEY ([NoteID]) REFERENCES [dbo].[Notes] ([NoteID]) ON DELETE CASCADE,
 CONSTRAINT [FK_NoteTags_Tag] FOREIGN KEY ([TagID]) REFERENCES [dbo].[Tags] ([TagID]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[Files](
    [FileID] [int] IDENTITY(1,1) NOT NULL,
    [NoteID] [int] NOT NULL,
    [FileName] [nvarchar](255) NOT NULL,
    [FilePath] [nvarchar](500) NOT NULL,
    [FileType] [nvarchar](50) NOT NULL,
    [FileSize] [bigint] NOT NULL,
    [UploadedAt] [datetime2](7) NULL DEFAULT GETDATE(),
 CONSTRAINT [PK_Files] PRIMARY KEY CLUSTERED ([FileID] ASC),
 CONSTRAINT [FK_Files_Note] FOREIGN KEY ([NoteID]) REFERENCES [dbo].[Notes] ([NoteID]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[Links](
    [LinkID] [int] IDENTITY(1,1) NOT NULL,
    [SourceNoteID] [int] NOT NULL,
    [TargetNoteID] [int] NOT NULL,
    [LinkType] [nvarchar](20) NOT NULL DEFAULT 'manual',
 CONSTRAINT [PK_Links] PRIMARY KEY CLUSTERED ([LinkID] ASC),
 CONSTRAINT [UQ_Source_Target] UNIQUE NONCLUSTERED ([SourceNoteID] ASC, [TargetNoteID] ASC),
 CONSTRAINT [FK_Links_Source] FOREIGN KEY ([SourceNoteID]) REFERENCES [dbo].[Notes] ([NoteID]),
 CONSTRAINT [FK_Links_Target] FOREIGN KEY ([TargetNoteID]) REFERENCES [dbo].[Notes] ([NoteID])
);

CREATE TABLE [dbo].[Reminders](
    [ReminderID] [int] IDENTITY(1,1) NOT NULL,
    [NoteID] [int] NOT NULL,
    [ReminderDate] [datetime2](7) NOT NULL,
    [IsCompleted] [bit] NULL DEFAULT 0,
    [NotifiedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_Reminders] PRIMARY KEY CLUSTERED ([ReminderID] ASC),
 CONSTRAINT [FK_Reminders_Note] FOREIGN KEY ([NoteID]) REFERENCES [dbo].[Notes] ([NoteID]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[Entities](
    [EntityID] [int] IDENTITY(1,1) NOT NULL,
    [NoteID] [int] NOT NULL,
    [EntityType] [nvarchar](50) NOT NULL,
    [EntityValue] [nvarchar](1000) NOT NULL,
    [PositionStart] [int] NULL,
    [PositionEnd] [int] NULL,
 CONSTRAINT [PK_Entities] PRIMARY KEY CLUSTERED ([EntityID] ASC),
 CONSTRAINT [FK_Entities_Note] FOREIGN KEY ([NoteID]) REFERENCES [dbo].[Notes] ([NoteID]) ON DELETE CASCADE,
 CONSTRAINT [CK_Entity_Type] CHECK ([EntityType] IN ('email','phone','date','url','keyword'))
);

-- 4. Создание дополнительных индексов для производительности
CREATE NONCLUSTERED INDEX [IX_Notes_UserID] ON [dbo].[Notes] ([UserID]);
CREATE NONCLUSTERED INDEX [IX_Notes_ParentNoteID] ON [dbo].[Notes] ([ParentNoteID]);
CREATE NONCLUSTERED INDEX [IX_NoteTags_TagID] ON [dbo].[NoteTags] ([TagID]);
CREATE NONCLUSTERED INDEX [IX_Files_NoteID] ON [dbo].[Files] ([NoteID]);
CREATE NONCLUSTERED INDEX [IX_Links_Source] ON [dbo].[Links] ([SourceNoteID]);
CREATE NONCLUSTERED INDEX [IX_Links_Target] ON [dbo].[Links] ([TargetNoteID]);
CREATE NONCLUSTERED INDEX [IX_Reminders_NoteID] ON [dbo].[Reminders] ([NoteID]);
CREATE NONCLUSTERED INDEX [IX_Reminders_Date] ON [dbo].[Reminders] ([ReminderDate]) WHERE [IsCompleted] = 0;
CREATE NONCLUSTERED INDEX [IX_Entities_NoteID] ON [dbo].[Entities] ([NoteID]);
CREATE NONCLUSTERED INDEX [IX_Entities_Type] ON [dbo].[Entities] ([EntityType]);

-- ============================================
-- 5. Вставка данных
-- ============================================

-- 5.1. Пользователи (пароль 'password' для всех, кроме admin – 'admin123')
SET IDENTITY_INSERT [dbo].[Users] ON;
INSERT [dbo].[Users] ([UserID], [Username], [PasswordHash], [FullName], [CreatedAt], [Role], [IsLocked]) VALUES
(1, N'alexey', N'5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8', N'Алексей Иванов', CAST(N'2025-01-10T10:00:00.0000000' AS DateTime2), N'Admin', 0),
(2, N'maria', N'5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8', N'Мария Петрова', CAST(N'2025-02-15T14:30:00.0000000' AS DateTime2), N'User', 0),
(3, N'ivan', N'5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8', N'Иван Смирнов', CAST(N'2025-03-20T11:00:00.0000000' AS DateTime2), N'User', 0);
SET IDENTITY_INSERT [dbo].[Users] OFF;

-- 5.2. Теги (осмысленные категории)
SET IDENTITY_INSERT [dbo].[Tags] ON;
INSERT [dbo].[Tags] ([TagID], [TagName]) VALUES
(1, N'Важное'),
(2, N'Идеи'),
(3, N'Проекты'),
(4, N'Личное'),
(5, N'Работа'),
(6, N'Учёба'),
(7, N'ИИ'),
(8, N'WPF'),
(9, N'Базы данных'),
(10, N'Документация'),
(11, N'Напоминание');
SET IDENTITY_INSERT [dbo].[Tags] OFF;

-- 5.3. Заметки (с иерархией, связями, напоминаниями)
SET IDENTITY_INSERT [dbo].[Notes] ON;
INSERT [dbo].[Notes] ([NoteID], [UserID], [Title], [Content], [ParentNoteID], [CreatedAt], [UpdatedAt], [IsDeleted], [ReminderDate], [IsReminderCompleted], [ReminderNote]) VALUES
-- Пользователь alexey (UserID = 1)
(1, 1, N'План дипломного проекта', 
 N'Основные этапы разработки приложения MindKeeper: 
 1. Проектирование базы данных (SQL Server).
 2. Разработка WPF-интерфейса с Material Design.
 3. Реализация CRUD заметок, тегов, файлов.
 4. Интеграция ИИ-функций: [[авто-тегирование]] и [[конспект]].
 5. Создание механизма связей [[граф знаний]].', 
 NULL, CAST(N'2025-02-01T09:00:00.0000000' AS DateTime2), CAST(N'2026-05-15T12:00:00.0000000' AS DateTime2), 0, NULL, 0, NULL),

(2, 1, N'Авто-тегирование', 
 N'Алгоритм выделяет ключевые слова из текста (длина >3, не стоп-слова) и создаёт соответствующие теги.', 
 1, CAST(N'2025-02-05T11:20:00.0000000' AS DateTime2), CAST(N'2026-05-14T13:30:00.0000000' AS DateTime2), 0, NULL, 0, NULL),

(3, 1, N'Конспект через ИИ', 
 N'Кнопка "Сделать конспект" генерирует краткую выжимку (первые 2 предложения). В будущем – вызов YandexGPT.', 
 1, CAST(N'2025-02-06T10:15:00.0000000' AS DateTime2), CAST(N'2026-05-14T14:00:00.0000000' AS DateTime2), 0, NULL, 0, NULL),

(4, 1, N'Граф знаний', 
 N'Связи между заметками создаются через [[Название заметки]]. Список связанных заметок отображается на вкладке "Связанные заметки".', 
 1, CAST(N'2025-02-07T09:30:00.0000000' AS DateTime2), CAST(N'2026-05-15T08:00:00.0000000' AS DateTime2), 0, NULL, 0, NULL),

(5, 1, N'Напоминания', 
 N'Установите дату и время, укажите текст. Когда время наступит, появится всплывающее уведомление. Просмотр в календаре.', 
 NULL, CAST(N'2025-02-10T16:45:00.0000000' AS DateTime2), CAST(N'2026-05-15T10:00:00.0000000' AS DateTime2), 0, CAST(N'2026-05-20T09:00:00.0000000' AS DateTime2), 0, N'Показать демо на защите'),

-- Пользователь maria (UserID = 2)
(6, 2, N'Книги по Python', 
 N'1. "Изучаем Python" – Марк Лутц
 2. "Python Crash Course" – Эрик Мэтиз
 3. "Effective Python" – Бретт Слаткин', 
 NULL, CAST(N'2025-02-20T18:00:00.0000000' AS DateTime2), CAST(N'2025-02-20T18:00:00.0000000' AS DateTime2), 0, NULL, 0, NULL),

(7, 2, N'Контакты коллег', 
 N'Алексей: alexey@example.com, +7 123 456-78-90
 Мария: maria@example.com, https://t.me/maria_p
 Иван: ivan@example.com', 
 NULL, CAST(N'2025-02-25T12:10:00.0000000' AS DateTime2), CAST(N'2025-02-25T12:10:00.0000000' AS DateTime2), 0, NULL, 0, NULL),

-- Пользователь ivan (UserID = 3)
(8, 3, N'Заметка 1: Идея для стартапа', 
 N'Мобильное приложение для [[управления задачами]] на основе ИИ. Основные фичи: голосовой ввод, автоматическое распределение по проектам.', 
 NULL, CAST(N'2025-03-25T09:00:00.0000000' AS DateTime2), CAST(N'2026-05-15T11:00:00.0000000' AS DateTime2), 0, NULL, 0, NULL),

(9, 3, N'Подзаметка: архитектура', 
 N'Использовать микросервисы: auth-service, task-service, ai-service. БД – PostgreSQL, кеш – Redis.', 
 8, CAST(N'2025-03-26T14:30:00.0000000' AS DateTime2), CAST(N'2026-05-14T09:00:00.0000000' AS DateTime2), 0, NULL, 0, NULL),

(10, 3, N'Управление задачами – требования', 
 N'Функции: создание, редактирование, дедлайны, приоритеты, напоминания, экспорт в PDF.', 
 8, CAST(N'2025-03-27T11:00:00.0000000' AS DateTime2), CAST(N'2026-05-14T10:00:00.0000000' AS DateTime2), 0, CAST(N'2026-06-01T10:00:00.0000000' AS DateTime2), 0, N'Проверить интеграцию с календарём');
SET IDENTITY_INSERT [dbo].[Notes] OFF;

-- 5.4. Привязка тегов к заметкам
INSERT [dbo].[NoteTags] ([NoteID], [TagID]) VALUES
(1, 3), (1, 7), (1, 8),   -- План проекта: Проекты, ИИ, WPF
(2, 2), (2, 7),           -- Авто-тегирование: Идеи, ИИ
(3, 7), (3, 1),           -- Конспект: ИИ, Важное
(4, 3), (4, 1),           -- Граф знаний: Проекты, Важное
(5, 1), (5, 11),          -- Напоминания: Важное, Напоминание
(6, 6),                   -- Книги Python: Учёба
(7, 4),                   -- Контакты: Личное
(8, 2), (8, 1),           -- Идея стартапа: Идеи, Важное
(9, 8), (9, 9),           -- Архитектура: WPF? нет, лучше "Базы данных"
(10, 1), (10, 11);        -- Требования: Важное, Напоминание

-- 5.5. Связи между заметками (граф знаний)
INSERT [dbo].[Links] ([SourceNoteID], [TargetNoteID], [LinkType]) VALUES
(1, 2, N'auto'),   -- План проекта ссылается на Авто-тегирование
(1, 3, N'auto'),   -- на Конспект
(1, 4, N'auto'),   -- на Граф знаний
(8, 9, N'auto');   -- Идея стартапа ссылается на Архитектуру

-- 5.6. Прикреплённые файлы (демонстрационные, пути условные)
INSERT [dbo].[Files] ([NoteID], [FileName], [FilePath], [FileType], [FileSize], [UploadedAt]) VALUES
(1, N'ТЗ_диплом.pdf', N'C:\KnowledgeApp\Attachments\ТЗ_диплом.pdf', N'pdf', 204800, GETDATE()),
(1, N'архитектура.png', N'C:\KnowledgeApp\Attachments\arch.png', N'png', 512000, GETDATE()),
(7, N'контакты.vcf', N'C:\KnowledgeApp\Attachments\contacts.vcf', N'vcf', 1024, GETDATE());

-- 5.7. Напоминания (отдельная таблица, но у нас уже есть ReminderDate в Notes)
-- Дублировать не будем. У заметок 5 и 10 уже заданы напоминания через ReminderDate.

-- 5.8. Извлечённые сущности (ИИ)
INSERT [dbo].[Entities] ([NoteID], [EntityType], [EntityValue], [PositionStart], [PositionEnd]) VALUES
(1, N'keyword', N'WPF', 80, 83),
(1, N'keyword', N'база данных', 45, 57),
(2, N'keyword', N'алгоритм', 10, 18),
(7, N'email', N'alexey@example.com', 15, 33),
(7, N'phone', N'+7 123 456-78-90', 45, 60),
(7, N'url', N'https://t.me/maria_p', 90, 115),
(8, N'url', N'http://startup.ru', 10, 25),
(10, N'date', N'2026-06-01', 150, 160);

-- 5.9. Индексы полнотекстового поиска (опционально, не обязательно)
-- CREATE FULLTEXT CATALOG ftCatalog AS DEFAULT;
-- CREATE FULLTEXT INDEX ON Notes(Content) KEY INDEX PK_Notes WITH STOPLIST = SYSTEM;

PRINT N'База данных PersonalKnowledgeDB успешно создана и заполнена тестовыми данными.';
GO