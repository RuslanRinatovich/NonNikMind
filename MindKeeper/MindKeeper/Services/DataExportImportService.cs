using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using Newtonsoft.Json;
using MindKeeper.Entity;
using SysIO = System.IO;

namespace MindKeeper.Services
{
    public static class DataExportImportService
    {
        // ==================== ЭКСПОРТ ====================

        public static bool ExportNotesToCsv(List<Note> notes)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = $"notes_backup_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (saveDialog.ShowDialog() != true) return false;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("\"NoteID\",\"Title\",\"Content\",\"CreatedAt\",\"UpdatedAt\",\"ParentNoteID\",\"Tags\"");

                foreach (var note in notes)
                {
                    var tags = note.Tags != null ? string.Join(";", note.Tags.Select(t => t.TagName)) : "";
                    sb.AppendLine($"\"{note.NoteID}\",\"{EscapeCsv(note.Title)}\",\"{EscapeCsv(note.Content)}\",\"{note.CreatedAt}\",\"{note.UpdatedAt}\",\"{note.ParentNoteID}\",\"{EscapeCsv(tags)}\"");
                }
                SysIO.File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка экспорта CSV: {ex.Message}", "Ошибка");
                return false;
            }
        }

        public static bool ExportNotesToJson(List<Note> notes)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = ".json",
                FileName = $"notes_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };
            if (saveDialog.ShowDialog() != true) return false;

            try
            {
                var exportData = notes.Select(n => new NoteExportDto
                {
                    NoteID = n.NoteID,
                    Title = n.Title,
                    Content = n.Content,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdatedAt,
                    ParentNoteID = n.ParentNoteID,
                    Tags = n.Tags?.Select(t => t.TagName).ToList() ?? new List<string>()
                }).ToList();
                var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                SysIO.File.WriteAllText(saveDialog.FileName, json, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка экспорта JSON: {ex.Message}", "Ошибка");
                return false;
            }
        }

        // ==================== ИМПОРТ ====================

        public static List<NoteExportDto> ImportNotesFromCsv()
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = ".csv"
            };
            if (openDialog.ShowDialog() != true) return null;

            try
            {
                var lines = SysIO.File.ReadAllLines(openDialog.FileName, Encoding.UTF8);
                if (lines.Length < 2) return null;
                var notes = new List<NoteExportDto>();
                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = ParseCsvLine(lines[i]);
                    if (parts.Length < 7) continue;

                    // Безопасный парсинг числовых полей
                    int noteId = 0;
                    int.TryParse(parts[0], out noteId);

                    int? parentId = null;
                    if (!string.IsNullOrWhiteSpace(parts[5]) && int.TryParse(parts[5], out int parsedParent))
                        parentId = parsedParent;

                    var note = new NoteExportDto
                    {
                        NoteID = noteId,
                        Title = parts[1],
                        Content = parts[2],
                        CreatedAt = DateTime.TryParse(parts[3], out var createdAt) ? createdAt : (DateTime?)null,
                        UpdatedAt = DateTime.TryParse(parts[4], out var updatedAt) ? updatedAt : (DateTime?)null,
                        ParentNoteID = parentId,
                        Tags = string.IsNullOrEmpty(parts[6]) ? new List<string>() : parts[6].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                    };
                    notes.Add(note);
                }
                return notes;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка импорта CSV: {ex.Message}", "Ошибка");
                return null;
            }
        }

        public static List<NoteExportDto> ImportNotesFromJson()
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = ".json"
            };
            if (openDialog.ShowDialog() != true) return null;

            try
            {
                var json = SysIO.File.ReadAllText(openDialog.FileName, Encoding.UTF8);
                var notes = JsonConvert.DeserializeObject<List<NoteExportDto>>(json);
                return notes;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка импорта JSON: {ex.Message}", "Ошибка");
                return null;
            }
        }

        /// <summary>
        /// Сохраняет импортированные заметки в БД, восстанавливает иерархию и привязывает теги.
        /// Генерирует уникальные заголовки с учётом ВСЕХ существующих заметок (включая удалённые).
        /// </summary>
        public static void SaveImportedNotesWithHierarchy(List<NoteExportDto> importedNotes, int userId)
        {
            if (importedNotes == null || importedNotes.Count == 0) return;

            var oldToNewId = new Dictionary<int, int>();

            using (var context = DataEntities.GetContext())
            {
                // (Опционально) Можно удалить все мягко удалённые заметки пользователя,
                // чтобы освободить заголовки. Раскомментируйте, если нужно.
                // var deletedNotes = context.Notes.Where(n => n.UserID == userId && n.IsDeleted == true);
                // context.Notes.RemoveRange(deletedNotes);
                // context.SaveChanges();

                // 1. Создаём заметки, генерируя уникальные заголовки (учитываем ВСЕ записи, включая удалённые)
                foreach (var dto in importedNotes)
                {
                    string baseTitle = dto.Title;
                    string uniqueTitle = baseTitle;
                    int counter = 1;

                    // Проверяем существование любой заметки (не только не удалённой) с таким заголовком
                    while (context.Notes.Any(n => n.UserID == userId && n.Title == uniqueTitle))
                    {
                        uniqueTitle = $"{baseTitle} ({counter++})";
                    }

                    var newNote = new Note
                    {
                        UserID = userId,
                        Title = uniqueTitle,
                        Content = dto.Content,
                        CreatedAt = dto.CreatedAt ?? DateTime.Now,
                        UpdatedAt = dto.UpdatedAt ?? DateTime.Now,
                        ParentNoteID = null,          // сначала без родителя
                        IsDeleted = false
                    };
                    context.Notes.Add(newNote);
                    context.SaveChanges(); // получаем новый NoteID

                    if (dto.NoteID != 0)
                        oldToNewId[dto.NoteID] = newNote.NoteID;

                    // 2. Обработка тегов для только что созданной заметки
                    if (dto.Tags != null && dto.Tags.Any())
                    {
                        var note = context.Notes.Find(newNote.NoteID);
                        foreach (var tagName in dto.Tags)
                        {
                            // Найти или создать тег (теги глобальные)
                            var tag = context.Tags.FirstOrDefault(t => t.TagName == tagName);
                            if (tag == null)
                            {
                                tag = new Tag { TagName = tagName };
                                context.Tags.Add(tag);
                                context.SaveChanges();
                            }
                            // Привязать тег к заметке, если ещё не привязан
                            if (!note.Tags.Any(t => t.TagID == tag.TagID))
                            {
                                note.Tags.Add(tag);
                            }
                        }
                        context.SaveChanges();
                    }
                }

                // 3. Обновляем ParentNoteID (после того, как все заметки созданы)
                foreach (var dto in importedNotes)
                {
                    if (dto.ParentNoteID.HasValue && oldToNewId.ContainsKey(dto.ParentNoteID.Value))
                    {
                        if (oldToNewId.ContainsKey(dto.NoteID))
                        {
                            var note = context.Notes.Find(oldToNewId[dto.NoteID]);
                            if (note != null)
                            {
                                note.ParentNoteID = oldToNewId[dto.ParentNoteID.Value];
                            }
                        }
                    }
                }
                context.SaveChanges();
            }
        }

        // ==================== Вспомогательные методы ====================

        private static string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\"", "\"\"");
            return text;
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (line[i] == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(line[i]);
                }
            }
            result.Add(sb.ToString());
            return result.ToArray();
        }
    }

    // DTO для экспорта/импорта
    public class NoteExportDto
    {
        public int NoteID { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? ParentNoteID { get; set; }
        public List<string> Tags { get; set; }
    }
}