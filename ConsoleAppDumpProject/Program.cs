using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace dump_script;

internal static class Program
{
    private const int DefaultMaxSizeKb = 1024;

    // Имя выходного файла по умолчанию
    private const string DefaultOutputFile = "project_structure.txt";

    // Сравнение путей: регистрозависимо на *nix, регистронезависимо на Windows
    private static StringComparison PathComparison =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    // Мусорные папки/файлы, которые пропускаются по умолчанию
    private static readonly HashSet<string> DefaultExcludes = new(StringComparer.OrdinalIgnoreCase)
    {
        // C# / .NET
        "bin", "obj", ".vs", "packages", "TestResults",
        // Python
        "__pycache__", ".venv", "venv", "env", ".pytest_cache", ".mypy_cache",
        ".tox", "build", "dist", "*.egg-info",
        // Node / VCS / IDE
        "node_modules", ".git", ".hg", ".svn", ".idea", ".vscode",
        // Мусор ОС
        ".DS_Store", "Thumbs.db",
    };

    // Расширения, которые считаем бинарными (содержимое не читаем)
    private static readonly HashSet<string> BinaryExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".tiff",
        ".pdf", ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz",
        ".exe", ".dll", ".so", ".dylib", ".pdb", ".obj", ".o", ".a", ".lib",
        ".pyc", ".pyo", ".class", ".jar", ".war", ".apk", ".ipa",
        ".mp3", ".mp4", ".avi", ".mov", ".mkv", ".wav", ".flac",
        ".woff", ".woff2", ".ttf", ".otf", ".eot",
        ".db", ".sqlite", ".sqlite3", ".mdf", ".ldf",
        ".nupkg", ".snupkg",
    };

    // ------------------------------------------------------------------
    // Точка входа
    // ------------------------------------------------------------------

    private static int Main(string[] argv)
    {
        // Обязательно ДО любого Encoding.GetEncoding(...)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Options? opts;
        try { opts = ParseArgs(argv); }
        catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); return 2; }
        if (opts is null) return 0; // --help

        var src = Path.GetFullPath(opts.Source);
        if (!Directory.Exists(src))
        {
            Console.Error.WriteLine($"Ошибка: '{src}' не является директорией");
            return 1;
        }

        var outPath = Path.GetFullPath(opts.Output);
        if (IsSubPathOf(outPath, src))
            Console.WriteLine(
                $"Выходной файл внутри проекта ({outPath}) — будет исключён из обхода");

        var excludes = new HashSet<string>(DefaultExcludes, StringComparer.OrdinalIgnoreCase);
        if (opts.ExtraExcludes is { Length: > 0 })
            foreach (var e in opts.ExtraExcludes)
                if (!string.IsNullOrWhiteSpace(e)) excludes.Add(e.Trim());

        HashSet<string>? exts = null;
        if (opts.Exts is { Length: > 0 })
        {
            exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in opts.Exts)
            {
                var t = e.Trim();
                if (t.Length == 0) continue;
                exts.Add(t.StartsWith('.') ? t.ToLowerInvariant() : "." + t.ToLowerInvariant());
            }
        }

        Console.WriteLine($"Источник: {src}");
        Console.WriteLine($"Результат: {outPath}");

        try
        {
            DumpProject(src, outPath, excludes, exts, opts.MaxSizeKb, withContent: !opts.NoContent);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка: {ex.Message}");
            return 1;
        }

        var sizeKb = new FileInfo(outPath).Length / 1024;
        Console.WriteLine($"Готово. Размер дампа: {sizeKb} КБ");
        return 0;
    }

    // ------------------------------------------------------------------
    // Основной дамп
    // ------------------------------------------------------------------

    private static void DumpProject(
        string src,
        string outPath,
        HashSet<string> excludes,
        HashSet<string>? exts,
        int maxSizeKb,
        bool withContent)
    {
        var files = CollectFiles(src, excludes, exts, outPath);
        int maxBytes = maxSizeKb * 1024;

        // NewLine = "\n" — единый формат переводов строк на всех ОС
        using var w = new StreamWriter(outPath, false, new UTF8Encoding(false))
        {
            NewLine = "\n"
        };
        void W(string s = "") => w.WriteLine(s);
        void Sep() => W(new string('=', 80));

        Sep();
        W($"ДАМП ПРОЕКТА: {src}");
        W($"Всего файлов (для вывода содержимого): {files.Count}");
        W($"Исключено папок/масок: {string.Join(", ", excludes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}");
        if (exts is not null)
            W($"Фильтр по расширениям: {string.Join(", ", exts.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}");
        W($"Лимит размера файла: {maxSizeKb} КБ");
        Sep();
        W();

        // -------- Дерево --------
        W("### СТРУКТУРА ПРОЕКТА ###");
        W();
        W(RootLabel(src));
        foreach (var line in RenderTree(src, excludes, string.Empty))
            W(line);
        W();
        W();

        if (!withContent) return;

        // -------- Содержимое --------
        W("### СОДЕРЖИМОЕ ФАЙЛОВ ###");
        W();
        for (int i = 0; i < files.Count; i++)
        {
            var fpath = files[i];
            var rel = Path.GetRelativePath(src, fpath).Replace('\\', '/');

            Sep();
            W($"[{i + 1}/{files.Count}] ФАЙЛ: {rel}");
            Sep();

            var (content, status) = TryReadText(fpath, maxBytes);
            if (content is null)
            {
                W(status);
                W();
                continue;
            }

            W($"# {status}");
            w.Write(NormalizeNewlines(content));
            if (!content.EndsWith("\n", StringComparison.Ordinal)) W();
            W();
        }
    }

    // ------------------------------------------------------------------
    // Дерево
    // ------------------------------------------------------------------

    private static IEnumerable<string> RenderTree(string dir, HashSet<string> excludes, string prefix)
    {
        var (dirs, files) = ListEntries(dir, excludes);
        var entries = dirs.Cast<string>().Concat(files).ToList();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            bool last = i == entries.Count - 1;
            string conn = last ? "└── " : "├── ";
            bool isDir = IsDirectory(entry);

            yield return prefix + conn + Path.GetFileName(entry) + (isDir ? "/" : "");

            if (isDir)
            {
                var childPrefix = prefix + (last ? "    " : "│   ");
                foreach (var line in RenderTree(entry, excludes, childPrefix))
                    yield return line;
            }
        }
    }

    private static (List<string> Dirs, List<string> Files) ListEntries(string dir, HashSet<string> excludes)
    {
        var dirs = new List<string>();
        var files = new List<string>();

        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(dir); }
        catch (UnauthorizedAccessException) { return (dirs, files); }
        catch (System.Security.SecurityException) { return (dirs, files); }
        catch (IOException) { return (dirs, files); }

        foreach (var e in entries)
        {
            if (IsExcluded(e, excludes)) continue;

            FileAttributes attr;
            try { attr = File.GetAttributes(e); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            bool isDir = (attr & FileAttributes.Directory) != 0;
            bool isLink = (attr & FileAttributes.ReparsePoint) != 0;

            // Не заходим в симлинки на папки — защита от циклов
            if (isDir && isLink) continue;

            if (isDir) dirs.Add(e); else files.Add(e);
        }

        dirs.Sort(StringComparer.OrdinalIgnoreCase);
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return (dirs, files);
    }

    private static bool IsDirectory(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.Directory) != 0; }
        catch { return false; }
    }

    // ------------------------------------------------------------------
    // Сбор файлов
    // ------------------------------------------------------------------

    private static List<string> CollectFiles(
        string root,
        HashSet<string> excludes,
        HashSet<string>? exts,
        string? skipFullPath)
    {
        var result = new List<string>();

        void Walk(string dir)
        {
            var (dirs, files) = ListEntries(dir, excludes);

            foreach (var f in files)
            {
                if (skipFullPath is not null &&
                    string.Equals(f, skipFullPath, PathComparison))
                    continue;

                if (exts is not null && !exts.Contains(Path.GetExtension(f)))
                    continue;

                result.Add(f);
            }

            foreach (var d in dirs) Walk(d);
        }

        Walk(root);
        return result;
    }

    private static bool IsExcluded(string path, HashSet<string> excludes)
    {
        var name = Path.GetFileName(path);
        foreach (var ex in excludes)
        {
            if (ex.Length == 0) continue;

            if (ex[0] == '*')
            {
                // "*" => исключить всё; "*.ext" => по суффиксу
                if (name.EndsWith(ex[1..], StringComparison.OrdinalIgnoreCase)) return true;
            }
            else if (string.Equals(name, ex, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Чтение файлов
    // ------------------------------------------------------------------

    private static (string? Content, string Status) TryReadText(string path, int maxBytes)
    {
        long size;
        try { size = new FileInfo(path).Length; }
        catch (Exception ex) { return (null, $"<ошибка доступа: {ex.Message}>"); }

        if (size > maxBytes)
            return (null, $"<файл слишком большой: {size} байт, пропущен>");

        if (BinaryExts.Contains(Path.GetExtension(path)))
            return (null, "<бинарный файл, содержимое не выводится>");

        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception ex) { return (null, $"<ошибка чтения: {ex.Message}>"); }

        int sniff = Math.Min(bytes.Length, 4096);

        // 1) UTF-16 без BOM: асимметричное расположение нулевых байт
        if (LooksLikeUtf16(bytes, sniff, out bool bigEndian))
        {
            var enc16 = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
            var text16 = enc16.GetString(bytes);
            return (text16, $"encoding=utf-16{(bigEndian ? "be" : "le")} (no BOM)");
        }

        // 2) null-byte sniff: если нули есть, но это не UTF-16 — бинарник
        for (int i = 0; i < sniff; i++)
            if (bytes[i] == 0)
                return (null, "<бинарный файл, содержимое не выводится>");

        // 3) Обычное определение кодировки
        var text = Decode(bytes, out var encName);
        return (text, $"encoding={encName}");
    }

    private static bool LooksLikeUtf16(byte[] b, int n, out bool bigEndian)
    {
        bigEndian = false;
        if (n < 4 || (n & 1) != 0) return false;

        int zerosEven = 0, zerosOdd = 0;
        for (int i = 0; i < n; i++)
            if (b[i] == 0)
            {
                if ((i & 1) == 0) zerosEven++;
                else zerosOdd++;
            }

        int threshold = n / 6;

        // UTF-16LE: ASCII-символы дают нулевой СТАРШИЙ (нечётный) байт
        if (zerosOdd > threshold && zerosEven < threshold / 2)
        {
            bigEndian = false;
            return true;
        }
        // UTF-16BE: нулевой ЧЁТНЫЙ байт
        if (zerosEven > threshold && zerosOdd < threshold / 2)
        {
            bigEndian = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Определяет кодировку текстового файла.
    /// Порядок: BOM → strict UTF-8 → CP1251.
    /// CP1251 покрывает все 256 значений, поэтому fallback не нужен.
    /// Замечание: файлы в CP1252 (западноевропейские) будут показаны как cp1251-мусор.
    /// </summary>
    private static string Decode(byte[] bytes, out string encodingName)
    {
        // BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encodingName = "utf-8 (BOM)";
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encodingName = "utf-16le";
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encodingName = "utf-16be";
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        // Строгий UTF-8: если байты валидны — это UTF-8
        try
        {
            var strictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
            var s = strictUtf8.GetString(bytes);
            encodingName = "utf-8";
            return s;
        }
        catch (DecoderFallbackException) { /* не UTF-8, идём дальше */ }

        // CP1251 — типичная кодировка старых C#-проектов (в т.ч. русских)
        encodingName = "cp1251";
        return Encoding.GetEncoding(1251).GetString(bytes);
    }

    private static string NormalizeNewlines(string s) =>
        s.Contains('\r') ? s.Replace("\r\n", "\n").Replace('\r', '\n') : s;

    // ------------------------------------------------------------------
    // Аргументы
    // ------------------------------------------------------------------

    private sealed class Options
    {
        public string Source = "";
        public string Output = DefaultOutputFile;   // "project_structure.txt"
        public string[]? ExtraExcludes;
        public string[]? Exts;
        public int MaxSizeKb = DefaultMaxSizeKb;
        public bool NoContent;
    }

    /// <summary>Возвращает null, если была запрошена справка (уже напечатана).</summary>
    private static Options? ParseArgs(string[] argv)
    {
        if (argv.Length == 0)
            throw new ArgumentException(
                "Укажите путь к проекту.\n" +
                "Пример: dump_script ./MyProject -o dump.txt\n" +
                "       dump_script . --ext .cs,.csproj --exclude bin,obj\n" +
                "Справка: dump_script --help");

        if (argv.Contains("-h") || argv.Contains("--help"))
        {
            PrintHelp();
            return null;
        }

        var o = new Options { Source = argv[0] };

        for (int i = 1; i < argv.Length; i++)
        {
            var a = argv[i];
            switch (a)
            {
                case "-o":
                case "--output":
                    o.Output = Require(argv, ++i, a);
                    break;

                case "--exclude":
                    o.ExtraExcludes = Require(argv, ++i, a)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries);
                    break;

                case "--ext":
                    o.Exts = Require(argv, ++i, a)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries);
                    break;

                case "--max-size":
                    if (!int.TryParse(Require(argv, ++i, a), out int kb) || kb < 0)
                        throw new ArgumentException($"Некорректное значение для {a}");
                    o.MaxSizeKb = kb;
                    break;

                case "--no-content":
                    o.NoContent = true;
                    break;

                default:
                    throw new ArgumentException($"Неизвестный аргумент: {a}");
            }
        }

        return o;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            "Использование:\n" +
            "  dump_script <source> [опции]\n" +
            "\n" +
            "Опции:\n" +
            $"  -o, --output <file>   Файл результата (по умолчанию {DefaultOutputFile})\n" +
            "  --exclude a,b,c       Доп. исключения (дополняют стандартные)\n" +
            "  --ext .cs,.csproj     Оставить только эти расширения\n" +
            "  --max-size <KB>       Лимит размера файла (по умолчанию 1024)\n" +
            "  --no-content          Только дерево, без содержимого\n" +
            "  -h, --help            Эта справка\n" +
            "\n" +
            "Примеры:\n" +
            "  dump_script ./MyCsApp\n" +
            "  dump_script ./MyCsApp -o cs_dump.txt\n" +
            "  dump_script ./MyCsApp --ext .cs,.csproj -o cs_sources.txt\n" +
            "  dump_script ./myapp_py -o py_dump.txt --exclude .venv,__pycache__\n" +
            "  dump_script ./MyCsApp --no-content -o tree.txt");
    }

    private static string Require(string[] argv, int idx, string flag)
    {
        if (idx >= argv.Length)
            throw new ArgumentException($"Не задано значение для {flag}");
        return argv[idx];
    }

    // ------------------------------------------------------------------
    // Утилиты
    // ------------------------------------------------------------------

    private static string RootLabel(string src)
    {
        var trimmed = src.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(name)) name = trimmed; // корень ФС: "/" или "C:"
        return name + "/";
    }

    private static bool IsSubPathOf(string child, string parent)
    {
        var c = Path.GetFullPath(child);
        var p = Path.GetFullPath(parent)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        return c.StartsWith(p, PathComparison);
    }
}
