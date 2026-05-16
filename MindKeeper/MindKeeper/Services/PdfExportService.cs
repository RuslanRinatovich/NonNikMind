using System;
using System.IO;
using Microsoft.Win32;
using MindKeeper.Entity;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using System.Linq;

namespace MindKeeper.Services
{
    public static class PdfExportService
    {
        public static bool ExportNoteToPdf(Note note)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                FileName = $"{SanitizeFileName(note.Title)}.pdf"
            };

            if (saveFileDialog.ShowDialog() != true)
                return false;

            try
            {
                // 1. Создаем документ MigraDoc
                Document document = new Document();
                Section section = document.AddSection();

                // 2. Заполняем документ содержимым заметки
                AddTitle(section, note);
                AddMetadata(section, note);
                AddContent(section, note);
                AddTags(section, note);

                // 3. Рендерим и сохраняем PDF
                // !!! ИСПРАВЛЕНО: Используем конструктор по умолчанию
                PdfDocumentRenderer renderer = new PdfDocumentRenderer();
                renderer.Document = document;
                renderer.RenderDocument();
                renderer.PdfDocument.Save(saveFileDialog.FileName);

                System.Windows.MessageBox.Show($"PDF-файл успешно создан:\n{saveFileDialog.FileName}",
                                "Экспорт завершён",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при создании PDF: {ex.Message}", "Ошибка",
                                                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        // Вспомогательные методы для создания документа
        private static void AddTitle(Section section, Note note)
        {
            Paragraph titleParagraph = section.AddParagraph(note.Title);
            titleParagraph.Format.Font.Size = 18;
            titleParagraph.Format.Font.Bold = true;
            titleParagraph.Format.SpaceAfter = Unit.FromCentimeter(1);
            titleParagraph.Format.Alignment = ParagraphAlignment.Center;
        }

        private static void AddMetadata(Section section, Note note)
        {
            Paragraph metaParagraph = section.AddParagraph();
            metaParagraph.Format.Font.Size = 9;
            metaParagraph.Format.Font.Color = Colors.Gray;
            metaParagraph.Format.SpaceAfter = Unit.FromCentimeter(1);
            metaParagraph.AddFormattedText($"ID: {note.NoteID}\nСоздана: {note.CreatedAt:dd.MM.yyyy HH:mm}\nИзменена: {note.UpdatedAt:dd.MM.yyyy HH:mm}");
        }

        private static void AddContent(Section section, Note note)
        {
            if (!string.IsNullOrWhiteSpace(note.Content))
            {
                Paragraph contentParagraph = section.AddParagraph(note.Content);
                contentParagraph.Format.Font.Size = 11;
                contentParagraph.Format.SpaceAfter = Unit.FromCentimeter(0.5);
                contentParagraph.Format.Alignment = ParagraphAlignment.Justify;
            }
            else
            {
                Paragraph emptyParagraph = section.AddParagraph("[Текст заметки отсутствует]");
                emptyParagraph.Format.Font.Italic = true;
                emptyParagraph.Format.Font.Color = Colors.Gray;
                emptyParagraph.Format.Alignment = ParagraphAlignment.Center;
            }
        }



    private static void AddTags(Section section, Note note)
    {
        if (note.Tags != null && note.Tags.Count > 0)
        {
            Paragraph tagsHeader = section.AddParagraph("Теги");
            tagsHeader.Format.Font.Size = 14;
            tagsHeader.Format.Font.Bold = true;
            tagsHeader.Format.SpaceAfter = Unit.FromCentimeter(0.5);

            Paragraph tagsList = section.AddParagraph();
            tagsList.Format.SpaceBefore = Unit.FromCentimeter(0.2);
            tagsList.Format.SpaceAfter = Unit.FromCentimeter(1);

            // Извлекаем названия тегов
            var tagNames = note.Tags.Select(t => t.TagName);
            tagsList.AddText(string.Join(", ", tagNames));
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