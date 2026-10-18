using FluentFTP;
using System;
using System.IO;
using System.Text.Json;

namespace PhoneSync
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== PhoneSync v1.0 ===");
            Console.WriteLine("Программа для синхронизации музыки с телефона по FTP\n");

            // Загрузка конфигурации
            Config config = LoadConfig();

            Console.WriteLine($"Подключение к {config.PhoneIp}:{config.PhonePort}...");
            Console.WriteLine($"Локальная папка: {config.LocalFolder}");
            Console.WriteLine($"Удаленная папка: {config.RemoteFolder}\n");

            try
            {
                // Подключение к FTP-серверу на телефоне
                using var client = new FtpClient(config.PhoneIp, config.FtpUser, config.FtpPassword, config.PhonePort);
                client.Connect();
                Console.WriteLine("✓ Подключение установлено!\n");

                // Создание локальной папки, если её нет
                Directory.CreateDirectory(config.LocalFolder);

                // Получение списка файлов в удаленной папке
                var files = client.GetListing(config.RemoteFolder);
                int downloadedCount = 0;

                foreach (var item in files)
                {
                    // Скачиваем только файлы (не папки)
                    if (item.Type == FtpObjectType.File)
                    {
                        string localPath = Path.Combine(config.LocalFolder, item.Name);
                        
                        // Проверка: если файл уже есть, пропускаем
                        if (File.Exists(localPath))
                        {
                            Console.WriteLine($"⊘ Пропущено (уже есть): {item.Name}");
                            continue;
                        }

                        Console.Write($"↓ Скачиваю: {item.Name}... ");
                        client.DownloadFile(localPath, item.FullName);
                        Console.WriteLine("✓");
                        downloadedCount++;
                    }
                }

                client.Disconnect();
                Console.WriteLine($"\n=== Готово! Скачано файлов: {downloadedCount} ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Ошибка: {ex.Message}");
                Console.WriteLine("Убедитесь, что:");
                Console.WriteLine("  1. FTP-сервер запущен на телефоне");
                Console.WriteLine("  2. Телефон и ПК в одной Wi-Fi сети");
                Console.WriteLine("  3. IP-адрес в config.json указан верно");
            }

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        static Config LoadConfig()
        {
            string configPath = "config.json";

            // Если конфиг не найден, создаем дефолтный
            if (!File.Exists(configPath))
            {
                var defaultConfig = new Config
                {
                    PhoneIp = "192.168.1.100",
                    PhonePort = 2121,
                    FtpUser = "anonymous",
                    FtpPassword = "",
                    RemoteFolder = "/sdcard/Music/",
                    LocalFolder = @"C:\Music"
                };

                string json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
                Console.WriteLine("Создан файл config.json с настройками по умолчанию.");
                Console.WriteLine("Отредактируйте его и запустите программу снова.\n");
                Console.WriteLine("Нажмите любую клавишу для выхода...");
                Console.ReadKey();
                Environment.Exit(0);
            }

            string jsonContent = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<Config>(jsonContent);
        }
    }

    class Config
    {
        public string PhoneIp { get; set; }
        public int PhonePort { get; set; }
        public string FtpUser { get; set; }
        public string FtpPassword { get; set; }
        public string RemoteFolder { get; set; }
        public string LocalFolder { get; set; }
    }
}