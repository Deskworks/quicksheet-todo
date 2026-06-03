using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// QuickSheet Todo Extension — task management in your spreadsheet cells.
/// Prefix: "todo"
/// 
/// Usage:
///   todo: list                          — show all tasks
///   todo: add Buy groceries             — add task (default priority: normal)
///   todo: add !high Fix login bug       — add with priority (!low, !normal, !high, !critical)
///   todo: add @2026-05-20 Ship v1.0     — add with due date
///   todo: add !high @2026-05-20 Deploy  — both priority and due date
///   todo: done 3                        — mark task #3 complete
///   todo: undo 3                        — mark task #3 incomplete
///   todo: rm 3                          — remove task #3
///   todo: clear done                    — remove all completed tasks
///   todo: stats                         — summary stats
/// 
/// Tasks persist to ~/.quicksheet/todo.csv between sessions.
/// </summary>
class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly List<TodoItem> Tasks = new();
    private static int _nextId = 1;
    private static string _dataFile = "";

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Persist tasks in user's home directory
        string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".quicksheet");
        Directory.CreateDirectory(dataDir);
        _dataFile = Path.Combine(dataDir, "todo.csv");

        LoadTasks();

        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                string? type = doc.RootElement.TryGetProperty("type", out var tp) ? tp.GetString() : null;

                switch (type)
                {
                    case "init":
                        HandleInit();
                        break;
                    case "activate":
                        HandleActivate(doc.RootElement);
                        break;
                }
            }
            catch (Exception ex)
            {
                SendError("", $"Parse error: {ex.Message}");
            }
        }
    }

    static void HandleInit()
    {
        SendJson(new
        {
            type = "register",
            prefix = "todo",
            name = "Todo Manager",
            version = "1.0.0"
        });
        SendLog($"Todo extension registered. {Tasks.Count} task(s) loaded.");
    }

    static void HandleActivate(JsonElement root)
    {
        string id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
        int gridCols = root.TryGetProperty("gridCols", out var gc) ? gc.GetInt32() : 5;
        int gridRows = root.TryGetProperty("gridRows", out var gr) ? gr.GetInt32() : 10;

        string[] extParams = [];
        if (root.TryGetProperty("params", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Array)
        {
            extParams = paramsProp.EnumerateArray()
                .Select(p => p.GetString() ?? "")
                .ToArray();
        }

        string command = extParams.Length > 0 ? extParams[0].Trim().ToLowerInvariant() : "list";
        string rest = extParams.Length > 1
            ? string.Join(" ", extParams.Skip(1)).Trim()
            : (extParams.Length > 0 ? string.Join(" ", extParams).Trim() : "");

        // Parse "command rest" from single param (e.g., "add Buy milk")
        if (extParams.Length == 1)
        {
            var parts = rest.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            command = parts.Length > 0 ? parts[0].ToLowerInvariant() : "list";
            rest = parts.Length > 1 ? parts[1] : "";
        }

        // Shortcut: bare number toggles task done/undone (e.g., "todo: 3")
        if (int.TryParse(command, out int toggleId) && string.IsNullOrWhiteSpace(rest))
        {
            var task = Tasks.FirstOrDefault(t => t.Id == toggleId);
            if (task != null)
            {
                ToggleTask(id, command, !task.Done, gridCols);
                return;
            }
        }

        switch (command)
        {
            case "list":
            case "ls":
            case "":
                ShowList(id, gridRows, gridCols);
                break;
            case "add":
            case "new":
                AddTask(id, rest, gridCols);
                break;
            case "done":
            case "check":
            case "x":
                ToggleTask(id, rest, true, gridCols);
                break;
            case "undo":
            case "uncheck":
                ToggleTask(id, rest, false, gridCols);
                break;
            case "rm":
            case "del":
            case "remove":
                RemoveTask(id, rest, gridCols);
                break;
            case "clear":
                ClearDone(id, rest, gridCols);
                break;
            case "stats":
            case "summary":
                ShowStats(id, gridCols);
                break;
            default:
                // Treat unknown command as "add <entire text>" (e.g., "todo: Buy milk")
                AddTask(id, $"{command} {rest}".Trim(), gridCols);
                break;
        }
    }

    static void AddTask(string id, string text, int gridCols)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            SendCells(id, new[] { Cell(0, 0, "❌ Usage: todo: add <task description>") });
            return;
        }

        var priority = Priority.Normal;
        DateOnly? dueDate = null;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Parse priority flags
        for (int i = words.Count - 1; i >= 0; i--)
        {
            var w = words[i].ToLowerInvariant();
            if (w is "!low") { priority = Priority.Low; words.RemoveAt(i); }
            else if (w is "!normal" or "!med") { priority = Priority.Normal; words.RemoveAt(i); }
            else if (w is "!high" or "!hi") { priority = Priority.High; words.RemoveAt(i); }
            else if (w is "!critical" or "!crit" or "!urgent") { priority = Priority.Critical; words.RemoveAt(i); }
        }

        // Parse @date
        for (int i = words.Count - 1; i >= 0; i--)
        {
            if (words[i].StartsWith('@') && DateOnly.TryParse(words[i][1..], out var d))
            {
                dueDate = d;
                words.RemoveAt(i);
            }
        }

        string title = string.Join(' ', words).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            SendCells(id, new[] { Cell(0, 0, "❌ Task title cannot be empty") });
            return;
        }

        var task = new TodoItem
        {
            Id = _nextId++,
            Title = title,
            Priority = priority,
            DueDate = dueDate,
            Done = false,
            CreatedAt = DateTime.Now
        };
        Tasks.Add(task);
        SaveTasks();

        var cells = new List<object>
        {
            Cell(0, 0, $"✅ Added #{task.Id}"),
            Cell(0, 1, task.Title),
        };
        if (gridCols >= 3) cells.Add(Cell(0, 2, PriorityLabel(task.Priority)));
        if (gridCols >= 4 && dueDate.HasValue) cells.Add(Cell(0, 3, $"📅 {dueDate:yyyy-MM-dd}"));
        cells.Add(Cell(1, 0, $"{Tasks.Count(t => !t.Done)} pending · {Tasks.Count(t => t.Done)} done"));

        SendCells(id, cells.ToArray());
    }

    static void ShowList(string id, int gridRows, int gridCols)
    {
        if (Tasks.Count == 0)
        {
            SendCells(id, new[]
            {
                Cell(0, 0, "📋 No tasks yet"),
                Cell(1, 0, "Use: todo: add <description>"),
                Cell(2, 0, "Flags: !high !low @2026-12-31")
            });
            return;
        }

        // Sort: incomplete first (by priority desc, then due date), then completed
        var sorted = Tasks
            .OrderBy(t => t.Done ? 1 : 0)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate ?? DateOnly.MaxValue)
            .ThenBy(t => t.Id)
            .ToList();

        var cells = new List<object>();
        // Header row
        cells.Add(Cell(0, 0, "#"));
        cells.Add(Cell(0, 1, "Task"));
        if (gridCols >= 3) cells.Add(Cell(0, 2, "Priority"));
        if (gridCols >= 4) cells.Add(Cell(0, 3, "Due"));
        if (gridCols >= 5) cells.Add(Cell(0, 4, "Status"));

        int maxRows = Math.Min(sorted.Count, gridRows - 2); // leave room for header + footer
        for (int i = 0; i < maxRows; i++)
        {
            int row = i + 1;
            var t = sorted[i];
            string check = t.Done ? "✓" : "○";
            string titleText = t.Done ? $"~{t.Title}~" : t.Title;
            bool isOverdue = !t.Done && t.DueDate.HasValue && t.DueDate.Value < DateOnly.FromDateTime(DateTime.Now);

            cells.Add(Cell(row, 0, $"{check} {t.Id}"));
            cells.Add(Cell(row, 1, titleText));
            if (gridCols >= 3) cells.Add(Cell(row, 2, PriorityLabel(t.Priority)));
            if (gridCols >= 4) cells.Add(Cell(row, 3, t.DueDate.HasValue ? (isOverdue ? $"⚠️ {t.DueDate:MM-dd}" : $"{t.DueDate:MM-dd}") : ""));
            if (gridCols >= 5) cells.Add(Cell(row, 4, t.Done ? "done" : "pending"));
        }

        // Footer
        int footerRow = maxRows + 1;
        int pending = Tasks.Count(t => !t.Done);
        int done = Tasks.Count(t => t.Done);
        int overdue = Tasks.Count(t => !t.Done && t.DueDate.HasValue && t.DueDate.Value < DateOnly.FromDateTime(DateTime.Now));
        string footer = $"{pending} pending · {done} done";
        if (overdue > 0) footer += $" · ⚠️ {overdue} overdue";
        if (sorted.Count > maxRows) footer += $" · (+{sorted.Count - maxRows} more)";
        cells.Add(Cell(footerRow, 0, footer));

        SendCells(id, cells.ToArray());
    }

    static void ToggleTask(string id, string rest, bool done, int gridCols)
    {
        if (!int.TryParse(rest.Trim(), out int taskId))
        {
            SendCells(id, new[] { Cell(0, 0, $"❌ Usage: todo: {(done ? "done" : "undo")} <task #>") });
            return;
        }

        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null)
        {
            SendCells(id, new[] { Cell(0, 0, $"❌ Task #{taskId} not found") });
            return;
        }

        task.Done = done;
        SaveTasks();

        string icon = done ? "✅" : "🔄";
        string status = done ? "completed" : "reopened";
        SendCells(id, new[]
        {
            Cell(0, 0, $"{icon} #{task.Id} {status}"),
            Cell(0, 1, task.Title),
            Cell(1, 0, $"{Tasks.Count(t => !t.Done)} pending · {Tasks.Count(t => t.Done)} done")
        });
    }

    static void RemoveTask(string id, string rest, int gridCols)
    {
        if (!int.TryParse(rest.Trim(), out int taskId))
        {
            SendCells(id, new[] { Cell(0, 0, "❌ Usage: todo: rm <task #>") });
            return;
        }

        int removed = Tasks.RemoveAll(t => t.Id == taskId);
        SaveTasks();

        if (removed == 0)
            SendCells(id, new[] { Cell(0, 0, $"❌ Task #{taskId} not found") });
        else
            SendCells(id, new[]
            {
                Cell(0, 0, $"🗑️ Removed #{taskId}"),
                Cell(1, 0, $"{Tasks.Count} task(s) remaining")
            });
    }

    static void ClearDone(string id, string rest, int gridCols)
    {
        if (rest.Trim().ToLowerInvariant() != "done")
        {
            SendCells(id, new[] { Cell(0, 0, "Usage: todo: clear done") });
            return;
        }

        int count = Tasks.RemoveAll(t => t.Done);
        SaveTasks();

        SendCells(id, new[]
        {
            Cell(0, 0, $"🧹 Cleared {count} completed task(s)"),
            Cell(1, 0, $"{Tasks.Count} task(s) remaining")
        });
    }

    static void ShowStats(string id, int gridCols)
    {
        int total = Tasks.Count;
        int done = Tasks.Count(t => t.Done);
        int pending = total - done;
        int overdueCount = Tasks.Count(t => !t.Done && t.DueDate.HasValue && t.DueDate.Value < DateOnly.FromDateTime(DateTime.Now));
        int critical = Tasks.Count(t => !t.Done && t.Priority == Priority.Critical);
        int high = Tasks.Count(t => !t.Done && t.Priority == Priority.High);
        double pct = total > 0 ? (double)done / total * 100 : 0;
        string bar = BuildProgressBar(total > 0 ? (double)done / total : 0, 15);

        var cells = new List<object>
        {
            Cell(0, 0, "📊 Todo Stats"),
            Cell(1, 0, $"Total: {total}"),
            Cell(1, 1, $"Done: {done}"),
            Cell(1, 2, $"Pending: {pending}"),
            Cell(2, 0, $"Progress: {pct:F0}%"),
            Cell(2, 1, bar),
        };
        if (overdueCount > 0) cells.Add(Cell(3, 0, $"⚠️ Overdue: {overdueCount}"));
        if (critical > 0) cells.Add(Cell(3, 1, $"🔴 Critical: {critical}"));
        if (high > 0) cells.Add(Cell(3, 2, $"🟠 High: {high}"));

        SendCells(id, cells.ToArray());
    }

    // --- Persistence (CSV) ---

    static void LoadTasks()
    {
        if (!File.Exists(_dataFile)) return;

        try
        {
            foreach (var line in File.ReadAllLines(_dataFile))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
                var parts = line.Split(',', 6);
                if (parts.Length < 6) continue;

                if (int.TryParse(parts[0], out int id)
                    && Enum.TryParse<Priority>(parts[2], true, out var pri)
                    && bool.TryParse(parts[4], out var done))
                {
                    var task = new TodoItem
                    {
                        Id = id,
                        Title = Unescape(parts[1]),
                        Priority = pri,
                        DueDate = DateOnly.TryParse(parts[3], out var d) ? d : null,
                        Done = done,
                        CreatedAt = DateTime.TryParse(parts[5], out var dt) ? dt : DateTime.Now
                    };
                    Tasks.Add(task);
                    if (id >= _nextId) _nextId = id + 1;
                }
            }
            SendLog($"Loaded {Tasks.Count} task(s) from {_dataFile}");
        }
        catch (Exception ex)
        {
            SendLog($"Warning: could not load tasks: {ex.Message}");
        }
    }

    static void SaveTasks()
    {
        try
        {
            var lines = Tasks.Select(t =>
                $"{t.Id},{Escape(t.Title)},{t.Priority},{(t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd") : "")},{t.Done},{t.CreatedAt:O}");
            File.WriteAllLines(_dataFile, new[] { "# id,title,priority,due,done,created" }.Concat(lines));
        }
        catch (Exception ex)
        {
            SendLog($"Warning: could not save tasks: {ex.Message}");
        }
    }

    static string Escape(string s) => s.Replace(",", ";;");
    static string Unescape(string s) => s.Replace(";;", ",");

    // --- Helpers ---

    static string PriorityLabel(Priority p) => p switch
    {
        Priority.Low => "🔵 low",
        Priority.Normal => "🟢 normal",
        Priority.High => "🟠 high",
        Priority.Critical => "🔴 critical",
        _ => "normal"
    };

    static string BuildProgressBar(double progress, int width)
    {
        int filled = (int)(progress * width);
        filled = Math.Clamp(filled, 0, width);
        return "[" + new string('█', filled) + new string('░', width - filled) + "]";
    }

    static object Cell(int r, int c, string v) => new { r, c, v };

    static void SendCells(string id, object[] cells)
    {
        SendJson(new { type = "write", id, cells });
    }

    static void SendJson(object obj)
    {
        string json = JsonSerializer.Serialize(obj, JsonOpts);
        Console.WriteLine(json);
        Console.Out.Flush();
    }

    static void SendError(string id, string message)
    {
        SendJson(new { type = "error", id, message });
    }

    static void SendLog(string message)
    {
        SendJson(new { type = "log", level = "info", message });
    }
}

enum Priority { Low, Normal, High, Critical }

class TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public Priority Priority { get; set; } = Priority.Normal;
    public DateOnly? DueDate { get; set; }
    public bool Done { get; set; }
    public DateTime CreatedAt { get; set; }
}
