namespace BankCore.App.Menus;

public static class ConsoleUi
{
    public static void Header(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔" + new string('═', 58) + "╗");
        Console.WriteLine("║  " + title.PadRight(56) + "║");
        Console.WriteLine("╚" + new string('═', 58) + "╝");
        Console.ResetColor();
    }

    public static void Success(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {msg}");
        Console.ResetColor();
    }

    public static void Error(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ✗ {msg}");
        Console.ResetColor();
    }

    public static void Info(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ► {msg}");
        Console.ResetColor();
    }

    public static void Separator()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  " + new string('─', 56));
        Console.ResetColor();
    }

    public static string Prompt(string label)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"  {label}: ");
        Console.ResetColor();
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    public static string PromptSecret(string label)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"  {label}: ");
        Console.ResetColor();
        var sb = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
            if (key.Key == ConsoleKey.Backspace && sb.Length > 0) { sb.Remove(sb.Length - 1, 1); Console.Write("\b \b"); }
            else if (key.Key != ConsoleKey.Backspace) { sb.Append(key.KeyChar); Console.Write('*'); }
        }
        return sb.ToString();
    }

    public static void PressAnyKey()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("\n  Press any key to continue...");
        Console.ResetColor();
        Console.ReadKey(true);
    }

    public static void Table(string[] headers, List<string[]> rows, int[] widths)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        string header = "  │ " + string.Join(" │ ", headers.Select((h, i) => h.PadRight(widths[i]))) + " │";
        Console.WriteLine("  ┌" + string.Join("┬", widths.Select(w => new string('─', w + 2))) + "┐");
        Console.WriteLine(header);
        Console.WriteLine("  ├" + string.Join("┼", widths.Select(w => new string('─', w + 2))) + "┤");
        Console.ForegroundColor = ConsoleColor.White;
        foreach (var row in rows)
        {
            string line = "  │ " + string.Join(" │ ", row.Select((c, i) =>
                (c ?? "").Length > widths[i] ? (c ?? "")[..widths[i]] : (c ?? "").PadRight(widths[i]))) + " │";
            Console.WriteLine(line);
        }
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("  └" + string.Join("┴", widths.Select(w => new string('─', w + 2))) + "┘");
        Console.ResetColor();
    }
}
