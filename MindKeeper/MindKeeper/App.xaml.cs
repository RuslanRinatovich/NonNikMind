using MindKeeper.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

using System.Windows;

namespace MindKeeper
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application

    {
        public static IAiService AiService { get; private set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ReminderService.Start();
            // Инициализация Gemini с вашим API-ключом
            var apiKey = ConfigurationManager.AppSettings["GeminiApiKey"]; // Лучше хранить в конфигурации!
            AiService = new GeminiService(apiKey);
        }
    }
}
