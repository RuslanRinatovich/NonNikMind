using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Xceed.Drawing;
using Microsoft.Win32;
using MindKeeper.Entity;
using Xceed.Document.NET; // Пространство имён для работы с документом
using Xceed.Words.NET;   // Пространство имён для основного класса DocX
using System.Windows;

namespace MindKeeper.Services
{
    public static class DocxExportService
    {
        public static bool ExportNoteToDocx(Entity.Note note)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Word documents (*.docx)|*.docx",
                DefaultExt = ".docx",
                FileName = $"{SanitizeFileName(note.Title)}.docx"
            };

            if (saveFileDialog.ShowDialog() != true)
                return false;

            try
            {
                // 1. Создаём документ с помощью DocX.Create
                using (var document = DocX.Create(saveFileDialog.FileName))
                {
                    // 2. Добавляем заголовок
                    var titleParagraph = document.InsertParagraph();
                    titleParagraph.Append(note.Title)
                                  .FontSize(18)
                                  .Bold()
                                  .Alignment = Alignment.center;

                    // 3. Пустая строка для отступа
                    document.InsertParagraph();

                    document.InsertParagraph($"ID заметки: {note.NoteID}").FontSize(9).Color(Color.Gray);
                    document.InsertParagraph($"Создана: {note.CreatedAt?.ToString("dd.MM.yyyy HH:mm")}")
                            .FontSize(9)
                            .Color(Xceed.Drawing.Color.Gray);
                    document.InsertParagraph($"Изменена: {note.UpdatedAt?.ToString("dd.MM.yyyy HH:mm")}")
                            .FontSize(9)
                            .Color(Xceed.Drawing.Color.Gray);

                    // 5. Основной контент заметки
                    if (!string.IsNullOrWhiteSpace(note.Content))
                    {
                        var contentParagraph = document.InsertParagraph();
                        contentParagraph.Append(note.Content)
                                        .FontSize(11)
                                        .Alignment = Alignment.both;
                    }
                    else
                    {
                        var emptyParagraph = document.InsertParagraph();
                        emptyParagraph.Append("[Текст заметки отсутствует]")
                                      .FontSize(11)
                                      .Italic()
                                      .Color(Xceed.Drawing.Color.Gray)
                                      .Alignment = Alignment.center;
                    }

                    document.InsertParagraph();

                    // 6. Список тегов
                    if (note.Tags != null && note.Tags.Any())
                    {
                        var tagsHeader = document.InsertParagraph();
                        tagsHeader.Append("Теги")
                                  .FontSize(14)
                                  .Bold()
                                  .SpacingAfter(6);

                        var tagsText = string.Join(", ", note.Tags.Select(t => t.TagName));
                        var tagsParagraph = document.InsertParagraph();
                        tagsParagraph.Append(tagsText)
                                     .FontSize(11);
                    }

                    document.Save();
                    System.Windows.MessageBox.Show($"DOCX-файл успешно создан:\n{saveFileDialog.FileName}",
                                                    "Экспорт завершён",
                                                    MessageBoxButton.OK,
                                                    MessageBoxImage.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при создании DOCX: {ex.Message}", "Ошибка",
                                                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName;
        }
    }
}