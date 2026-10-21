using FluentFTP;
using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhoneSync
{
    class Program
    {
        const string configPath = "config.json";
        static void Main(string[] args)
        {
            bool isSkipping = true;
            bool isRunning = true;
            bool isConnected = false;
            bool isValidConfig = true;
            FileFlags fileFlags = new FileFlags(
                true, true, true, true, true, true, 
                true, true, true, true, true);

            // Загрузка конфигурации
            Config config = LoadConfig();
            if (!IsValidConfig(config))
            {
                isValidConfig = false;
            }
            var client = new FtpClient();

            while (isRunning)
            {
                MenuAction menuAction = ShowMenu(isSkipping);

                switch (menuAction)
                {
                    case MenuAction.ToggleSkip:
                        isSkipping = !isSkipping;
                        continue;

                    case MenuAction.ConnectToPhone:
                        Console.Clear();
                        if (!isValidConfig)
                        {
                            Console.WriteLine("Файл конфигурации некорректен! Исправьте данные в нем");
                            Console.ReadKey();
                            continue;
                        }
                        Console.WriteLine($"Подключение к {config.PhoneIp}:{config.PhonePort}...");
                        Console.WriteLine($"Локальная папка: {config.LocalPath}");
                        Console.WriteLine($"Удаленная папка: {config.RemotePath}\n");

                        client?.Dispose();
                        client = GetConnection(config);
                        if (client != null)
                        {
                            isConnected = true;
                            DownloadFiles(config, client, isSkipping, fileFlags);
                        }
                        else
                        {
                            isConnected = false;
                        }
                        break;

                    case MenuAction.RewriteConfigDataPaths:
                        RewriteConfigDataPaths(config);
                        if (isConnected)
                        {
                            client?.Dispose();
                            client = GetConnection(config);
                        }
                        isValidConfig = true;
                        break;

                    case MenuAction.RewriteConfigData:
                        RewriteConfigData(config);
                        if (isConnected)
                        {
                            client?.Dispose();
                        }
                        isValidConfig = true;
                        break;

                    case MenuAction.EnterFileMenu:
                        ShowFileMenu(fileFlags);
                        break;

                    case MenuAction.Exit:
                        Console.WriteLine("Завершение работы...");
                        if (isConnected)
                        {
                            client?.Dispose();
                        }
                        isRunning = false;
                        break;

                    default:
                        break;
                }
            }
        }

        static Config LoadConfig()
        {
            // Если конфиг не найден, создаем дефолтный
            if (!File.Exists(configPath))
            {
                var defaultConfig = new Config
                {
                    PhoneIp = "192.168.1.100",
                    PhonePort = 2121,
                    FtpUser = "anonymous",
                    FtpPassword = "",
                    RemotePath = "/sdcard/Music/",
                    LocalPath = @"C:\Music"
                };

                string json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
                Console.WriteLine("Отсутствует файл конфигурации.");
                Console.WriteLine("Создан файл config.json с настройками по умолчанию.");
                Console.WriteLine("Отредактируйте его и запустите программу снова.\n");
                Console.WriteLine("Нажмите любую клавишу для выхода...");
                Console.ReadKey();
                Environment.Exit(0);
            }

            string jsonContent = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<Config>(jsonContent);
        }

        static void RewriteConfig(Config newConfig)
        {
            string json = JsonSerializer.Serialize(newConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }

        static void DownloadFiles(Config config, FtpClient client, bool isSkipping, FileFlags fileFlags)
        {
            Directory.CreateDirectory(config.LocalPath);
            var files = client.GetListing(config.RemotePath);
            int downloadedCount = 0;

            foreach (var item in files)
            {
                if (item.Type == FtpObjectType.File)
                {
                    string localPath = Path.Combine(config.LocalPath, item.Name);
                    string displayName = item.Name;
                    string extension = Path.GetExtension(item.Name);
                    if (!isValidFlags(extension, fileFlags)) continue;

                    if (File.Exists(localPath))
                    {
                        if (isSkipping)
                        {
                            Console.WriteLine($"[+] Пропущено (уже есть): {item.Name}");
                            continue;
                        }
                        else
                        {
                            localPath = GetUniqueFilePath(localPath);
                            displayName = Path.GetFileName(localPath);

                            Console.Write($"[+] Файл уже существует, сохраняем как '{displayName }'...");
                        }
                    }
                    else
                    {
                        Console.Write($"Скачиваю: {displayName}... ");
                    }

                        client.DownloadFile(localPath, item.FullName);
                    Console.WriteLine(" [+]");
                    downloadedCount++;
                }
            }
            Console.WriteLine($"\n=== Готово! Скачано файлов: {downloadedCount} ===");
            Console.ReadKey();
        }
        static string GetUniqueFilePath(string originalPath)
        {
            // Если файла нет, возвращаем исходный путь без изменений
            if (!File.Exists(originalPath))
                return originalPath;

            string directory = Path.GetDirectoryName(originalPath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);

            int counter = 1;
            string newPath;

            // Ищем первое свободное имя
            do
            {
                newPath = Path.Combine(directory, $"{fileNameWithoutExt} ({counter}){extension}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }

        static MenuAction ShowMenu(bool isSkipping)
        {
            while (true) // Крутимся, пока пользователь не введет корректную цифру
            {
                Console.Clear();
                Console.WriteLine("=== PhoneSync v1.1 ===");
                Console.WriteLine("Программа для синхронизации файлов с телефона по FTP\n");
                Console.WriteLine("1. Подключиться к телефону");
                if (isSkipping)
                {
                    Console.WriteLine("2. Пропускать файлы с одинаковым названием? [+]");
                }
                else
                {
                    Console.WriteLine("2. Пропускать файлы с одинаковым названием? [ ]");
                }
                Console.WriteLine("3. Изменить файл конфигурации полностью");
                Console.WriteLine("4. Изменить удаленную и локальную папки");
                Console.WriteLine("5. Войти в меню выбора расширений файлов для загрузки");
                Console.WriteLine("0. Выход из программы");
                Console.Write("Ваш выбор: ");
                string answer = Console.ReadLine();

                MenuAction action = answer switch
                {
                    "1" => MenuAction.ConnectToPhone,
                    "2" => MenuAction.ToggleSkip,
                    "3" => MenuAction.RewriteConfigData,
                    "4" => MenuAction.RewriteConfigDataPaths,
                    "5" => MenuAction.EnterFileMenu,
                    "0" => MenuAction.Exit,
                    _ => MenuAction.Invalid // Любой другой ввод
                };

                if (action != MenuAction.Invalid)
                {
                    return action;
                }
            }
        }

        static FileFlags ShowFileMenu(FileFlags fileFlags)
        {
            var menuItems = new (string Title, Func<FileFlags, bool> GetState, Action<FileFlags> Toggle)[]
            {
                ("Текстовые документы и офисные файлы", f => f.Documents,    f => f.Documents = !f.Documents),
                ("Изображения и графика",               f => f.Pictures,     f => f.Pictures = !f.Pictures),
                ("Аудиофайлы",                          f => f.Audio,        f => f.Audio = !f.Audio),
                ("Видеофайлы",                          f => f.Video,        f => f.Video = !f.Video),
                ("Архивы и образы дисков",              f => f.Archives,     f => f.Archives = !f.Archives),
                ("Исполняемые файлы и установщики",     f => f.Executables,  f => f.Executables = !f.Executables),
                ("Программирование и веб-разработка",   f => f.Development,  f => f.Development = !f.Development),
                ("Базы данных",                         f => f.Databases,    f => f.Databases = !f.Databases),
                ("Системные и конфигурационные файлы",  f => f.Sysconf,      f => f.Sysconf = !f.Sysconf),
                ("Сеть и интернет",                     f => f.Web,          f => f.Web = !f.Web),
                ("Другие",                              f => f.Other,        f => f.Other = !f.Other)
            };

            while (true)
            {
                Console.Clear();
                Console.WriteLine("Выберите какие файлы учитывать при скачивании:");
                Console.WriteLine("(По умолчанию выбраны все)\n");

                for (int i = 0; i < menuItems.Length; i++)
                {
                    var item = menuItems[i];
                    string status = item.GetState(fileFlags) ? "[+]" : "[ ]";
                    Console.WriteLine($"{i + 1,2}. {item.Title,-42} {status}"); 
                }

                Console.WriteLine("\n 0. Выход из меню");
                Console.Write("\nВаш выбор: ");

                string answer = Console.ReadLine();

                if (answer == "0") return fileFlags;

                if (int.TryParse(answer, out int choice) && choice >= 1 && choice <= menuItems.Length)
                {
                    menuItems[choice - 1].Toggle(fileFlags);
                }
            }
        }

        static FtpClient GetConnection(Config config)
        {
            try
            {
                // Подключение к FTP-серверу на телефоне
                var client = new FtpClient(config.PhoneIp, config.FtpUser, config.FtpPassword, config.PhonePort);
                client.Connect();
                Console.WriteLine("Подключение установлено!\n");
                return client;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Ошибка: {ex.Message}");
                Console.WriteLine("Убедитесь, что:");
                Console.WriteLine("  1. FTP-сервер запущен на телефоне");
                Console.WriteLine("  2. Телефон и ПК в одной Wi-Fi сети");
                Console.WriteLine("  3. IP-адрес в config.json указан верно");
            }
            return null;
        }

        public static void RewriteConfigData(Config config)
        {
            var newConfig = new Config
            {
                PhoneIp = "",
                PhonePort = 0,
                FtpUser = "",
                FtpPassword = "",
                LocalPath = "",
                RemotePath = ""
            };
            string phonePort;
            string errorText = null;

            Console.Clear();
            Console.WriteLine("Оставьте поля пустыми, если хотите оставить текущие значения");

            Console.WriteLine("Введите новый IP-адрес телефона: ");
            Console.WriteLine($"Текущий: {config.PhoneIp}");
            Console.Write("Новый: ");
            newConfig.PhoneIp = Console.ReadLine();
            Console.Clear();
            Console.WriteLine("Оставьте поля пустыми, если хотите оставить текущие значения");

            Console.WriteLine("Введите новый порт телефона: ");
            Console.WriteLine($"Текущий: {config.PhonePort}");
            Console.Write("Новый: ");
            phonePort = Console.ReadLine();
            Console.Clear();
            Console.WriteLine("Оставьте поля пустыми, если хотите оставить текущие значения");

            Console.WriteLine("Введите новое имя пользователя: ");
            Console.WriteLine($"Текущее: {config.FtpUser}");
            Console.Write("Новое: ");
            newConfig.FtpUser = Console.ReadLine();
            Console.Clear();
            Console.WriteLine("Оставьте поля пустыми, если хотите оставить текущие значения");

            Console.WriteLine("Введите новый пароль: ");
            Console.WriteLine($"Текущий: {config.FtpPassword}");
            Console.Write("Новый: ");
            newConfig.FtpPassword = Console.ReadLine();
            Console.Clear();
            Console.WriteLine("Оставьте поля пустыми, если хотите оставить текущие значения");

            Console.WriteLine("Введите новую локальную папку: ");
            Console.WriteLine($"Текущая: {config.LocalPath}");
            Console.Write("Новая: ");
            newConfig.LocalPath = Console.ReadLine();
            Console.Clear();
            Console.WriteLine("Оставьте поля пустыми, если хотите оставить текущие значения");

            Console.WriteLine("Введите новую удаленную папку: ");
            Console.WriteLine($"Текущая: {config.RemotePath}");
            Console.Write("Новая: ");
            newConfig.RemotePath = Console.ReadLine();
            Console.Clear();

            if (!string.IsNullOrWhiteSpace(phonePort))
            {
                IsValidPort(phonePort, out int port, out errorText);
                newConfig.PhonePort = port;
            }
            if (!string.IsNullOrWhiteSpace(newConfig.LocalPath)) IsValidLocalPath(newConfig.LocalPath, out errorText);
            if (!string.IsNullOrWhiteSpace(newConfig.RemotePath))
            {
                IsValidRemotePath(newConfig.RemotePath, out string remotePath, out errorText);
                newConfig.RemotePath = remotePath;
            }
            if (string.IsNullOrEmpty(errorText))
            {
                if (string.IsNullOrWhiteSpace(newConfig.PhoneIp)) newConfig.PhoneIp = config.PhoneIp;
                if (string.IsNullOrWhiteSpace(phonePort)) newConfig.PhonePort = config.PhonePort;
                if (string.IsNullOrWhiteSpace(newConfig.FtpUser)) newConfig.FtpUser = config.FtpUser;
                if (string.IsNullOrWhiteSpace(newConfig.FtpPassword)) newConfig.FtpPassword = config.FtpPassword;
                if (string.IsNullOrWhiteSpace(newConfig.LocalPath)) newConfig.LocalPath = config.LocalPath;
                if (string.IsNullOrWhiteSpace(newConfig.RemotePath)) newConfig.RemotePath = config.RemotePath;

                config = newConfig;
                RewriteConfig(config);
                Console.WriteLine("Готово!");
                Console.ReadKey();
                return;
            }
            else
            {
                Console.WriteLine($"Ошибка: {errorText}");
                Console.WriteLine("Проверьте правильность введенных данных и попробуйте еще раз");
                Console.ReadKey();
            }
        }
        // TODO: реализовать возможность переписать отдельные данные файла конфигурации 
        // Отказался от реализации ввиду заргуженности кода, и думаю в целом можно оптимизировать
        // данные методы перезаписи файла конфигурации, но сейчас острой необходимости в этом нет.
        // Функционала на данном этапе более чем хватает, и не сказать, что его реализация прям ужасна.
        //public void RewriteConfigPart(Config config)
        //{

        //}

        public static void RewriteConfigDataPaths(Config config)
        {
            string localPath;
            string remotePath;
            string errorText = null;

            Console.Clear();
            Console.WriteLine("Оставьте поля пустыми, если хотите оставить текущие значения");

            Console.WriteLine("Введите новую локальную папку: ");
            Console.WriteLine($"Текущая: {config.LocalPath}");
            Console.Write("Новая: ");
            localPath = Console.ReadLine();
            Console.Clear();
            Console.WriteLine("Оставьте поля пустыми, если хотите оставить текущие значения");

            Console.WriteLine("Введите новую удаленную папку: ");
            Console.WriteLine($"Текущая: {config.RemotePath}");
            Console.Write("Новая: ");
            remotePath = Console.ReadLine();
            Console.Clear();

            if (!string.IsNullOrWhiteSpace(localPath)) IsValidLocalPath(localPath, out errorText);
            if (!string.IsNullOrWhiteSpace(remotePath)) IsValidRemotePath(remotePath, out remotePath, out errorText);
            if (string.IsNullOrEmpty(errorText))
            {
                if (!string.IsNullOrWhiteSpace(localPath)) config.LocalPath = localPath;
                if (!string.IsNullOrWhiteSpace(remotePath)) config.RemotePath = remotePath;
                RewriteConfig(config);
                Console.WriteLine("Готово!");
                Console.ReadKey();
                return;
            }
            else
            {
                Console.WriteLine($"Ошибка: {errorText}");
                Console.WriteLine("Проверьте правильность введенных данных и попробуйте еще раз");
                Console.ReadKey();
            }
        }

        public static bool IsValidConfig(Config config)
        {
            string errorText;
            if(IsValidIpAddress(config.PhoneIp, out errorText) &&
                IsValidPort(config.PhonePort.ToString(), out int port, out errorText) && 
                IsValidLocalPath(config.LocalPath, out errorText) && 
                IsValidRemotePath(config.RemotePath, out string path, out errorText))
            {
                return true;
            }
            return false;
        }

        public static bool IsValidIpAddress(string ipAddress, out string errorText)
        {
            if (IpValidator.IpAddress().IsMatch(ipAddress))
            {
                errorText = null;
                return true;
            }
            errorText = "IP-адрес телефона должен быть вида: '***.***.***.***'";
            return false;
        }

        public static bool IsValidPort(string portString, out int port, out string errorText)
        {
            if (int.TryParse(portString, out port))
            {
                errorText = null;
                // Порт должен быть от 1 до 65535
                return port >= 1 && port <= 65535;
            }
            errorText = "Порт должен быть в пределах от 1 до 65535";
            return false;
        }

        public static bool IsValidRemotePath(string path, out string normalizedPath, out string errorText)
        {
            normalizedPath = null;
            errorText = "Путь удаленной папки не должен быть пустым";
            if (string.IsNullOrWhiteSpace(path))
                return false;

            // Нормализуем: убираем дублирующиеся слэши
            normalizedPath = Regex.Replace(path.Trim(), @"/{2,}", "/");
            errorText = "Путь удаленной папки должен быть меньше 260 символов";
            // Путь должен быть меньше 260 символов
            if (normalizedPath.Length > 260)
            {
                return false;
            }
            errorText = "Путь удаленной папки должен начинаться с /";
            // Должен начинаться с /
            if (!normalizedPath.StartsWith("/"))
                return false;

            errorText = "Путь удаленной папки не должен содержать две точки подряд '..'";
            // Запрещаем ..
            if (normalizedPath.Contains(".."))
                return false;

            errorText = "Сегменты пути удаленной папки не должны быть больше 255 символов и содержать запрещенные символы";
            // Проверяем каждый сегмент
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (segment.Length > 255)
                    return false;

                // Разрешенные символы в сегменте
                if (!Regex.IsMatch(segment, @"^[\w\s.\-]+$"))
                    return false;
            }
            errorText = null;
            return true;
        }

        public static bool IsValidLocalPath(string path, out string errorText)
        {
            errorText = "Путь локальной папки не должен быть пустым";
            if (string.IsNullOrWhiteSpace(path))
                return false;

            errorText = "Путь локальной папки должен начинаться с корня, например C:\\";
            // Проверяем, что путь начинается с корня (C:\, D:\ или \\server\)
            if (!Path.IsPathRooted(path))
                return false;

            errorText = "Путь локальной папки не должен содержать запрещенных символов";
            // Проверяем наличие запрещенных символов
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return false;

            errorText = "Корень локальной папки должен быть валидным";
            // Проверяем, что имя диска валидно
            string root = Path.GetPathRoot(path);
            if (root.Length > 3) // Формат "C:\" имеет длину 3
                return false;

            errorText = null;
            return true;
        }

        private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".doc", ".docx", ".pdf", ".rtf", ".xls", ".xlsx",
            ".ods", ".csv", ".ppt", ".pptx", ".odp", ".pdf", ".md",
            ".epub", ".mobi", ".fb2"
        };

        private static readonly HashSet<string> PictureExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp", 
            ".tiff", ".tif", ".ico", ".heic", ".heif", ".raw", 
            ".cr2", ".nef", ".arw", ".psd", ".ai"
        };

        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".wav", ".aiff", ".alac", ".flac", ".ape", ".mp3", ".aac", 
            ".ogg", ".m4a", ".wma", ".opus", ".mid", "midi"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", 
            ".webm", ".wmv", ".flv", ".3gp"
        };

        private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", 
            ".xz", ".iso", ".dmg", ".img"
        };

        private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".msi", ".app", ".apk", ".ipa", ".bat", ".cmd", 
            ".sh", ".ps1", ".deb", ".rpm", ".bin", ".run"
        };

        private static readonly HashSet<string> DevelopmentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".html", ".htm", ".css", ".js", ".ts", ".py", ".java", 
            ".c", ".cpp", ".h", ".hpp", ".cs", ".php", ".go", 
            ".rb", ".json", ".xml", ".yaml", ".yml", ".sql"
        };

        private static readonly HashSet<string> DatabaseExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".db", ".sqlite", ".sqlite3", ".mdb", 
            ".accdb", ".mysql", ".pgsql"
        };

        private static readonly HashSet<string> SysconfExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".dll", ".sys", ".drv", ".ini", ".cfg", ".conf", 
            ".config", ".reg", ".log", ".tmp", ".env", ".dat"
        };

        private static readonly HashSet<string> WebExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".url", ".webloc", ".torrent", ".eml", ".msg", ".vcf"
        };

        public static bool isValidFlags(string extension, FileFlags fileFlags)
        {
            if (fileFlags.Other) return true;

            return (fileFlags.Documents && DocumentExtensions.Contains(extension)) ||
                   (fileFlags.Pictures && PictureExtensions.Contains(extension)) ||
                   (fileFlags.Audio && AudioExtensions.Contains(extension)) ||
                   (fileFlags.Video && VideoExtensions.Contains(extension)) ||
                   (fileFlags.Archives && ArchiveExtensions.Contains(extension)) ||
                   (fileFlags.Executables && ExecutableExtensions.Contains(extension)) ||
                   (fileFlags.Development && DevelopmentExtensions.Contains(extension)) ||
                   (fileFlags.Databases && DatabaseExtensions.Contains(extension)) ||
                   (fileFlags.Sysconf && SysconfExtensions.Contains(extension)) ||
                   (fileFlags.Web && WebExtensions.Contains(extension));
        }
    }

    public enum MenuAction
    {
        ConnectToPhone,
        RewriteConfigData,
        RewriteConfigDataPaths,
        ToggleSkip,
        EnterFileMenu,
        Exit,
        Invalid
    }

    public static partial class IpValidator
    {
        [GeneratedRegex(@"^(\d{1,3}\.){3}\d{1,3}$")]
        public static partial Regex IpAddress();
    }

    class FileFlags
    {
        public bool Documents { get; set; }
        public bool Pictures { get; set; }
        public bool Audio { get; set; }
        public bool Video { get; set; }
        public bool Archives { get; set; }
        public bool Executables {  get; set; }
        public bool Development { get; set; }
        public bool Databases { get; set; }
        public bool Sysconf {  get; set; }
        public bool Web {  get; set; }
        public bool Other { get; set; }
        public FileFlags(bool documents, bool pictures, bool audio, bool video, 
            bool archives, bool executables, bool development, 
            bool databases, bool sysconf, bool web, bool other)
        {
            Documents = documents;
            Pictures = pictures;
            Audio = audio;
            Video = video;
            Archives = archives;
            Executables = executables;
            Development = development;
            Databases = databases;
            Sysconf = sysconf;
            Web = web;
            Other = other;
        }
    }

    class Config
    {
        public string PhoneIp { get; set; }
        public int PhonePort { get; set; }
        public string FtpUser { get; set; }
        public string FtpPassword { get; set; }
        public string RemotePath { get; set; }
        public string LocalPath { get; set; }
    }
}