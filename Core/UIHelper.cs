using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantResearchAgent.Core;

/// <summary>
/// Helper class for creating beautiful CLI UI using Spectre.Console
/// </summary>
public static class UIHelper
{
    // Color scheme
    public static readonly Color PrimaryColor = Color.Cyan1;
    public static readonly Color SecondaryColor = Color.DodgerBlue2;
    public static readonly Color AccentColor = Color.Magenta1;
    public static readonly Color SuccessColor = Color.Green;
    public static readonly Color WarningColor = Color.Yellow;
    public static readonly Color ErrorColor = Color.Red;
    public static readonly Color MutedColor = Color.Grey;
    
    /// <summary>
    /// Display the main application banner
    /// </summary>
    public static void ShowBanner()
    {
        AnsiConsole.Clear();
        
        var banner = new FigletText("FeenQR")
            .Centered()
            .Color(PrimaryColor);
        
        AnsiConsole.Write(banner);
        
        var subtitle = new Markup($"[{PrimaryColor}]Quantitative Research Agent[/] [dim]| v2.0.0[/]\n").Centered();
        AnsiConsole.Write(subtitle);
        AnsiConsole.WriteLine();
    }
    
    /// <summary>
    /// Display the main menu with categories
    /// </summary>
    public static string ShowMainMenu()
    {
        ShowBanner();
        
        // Quick Actions Panel
        var quickPanel = new Panel(
            new Markup(
                "[cyan1]chat[/]          AI Assistant (natural language queries)\n" +
                "[cyan1]analyze <SYM>[/]  Quick comprehensive analysis\n" +
                "[cyan1]news <SYM>[/]     Latest news & sentiment"
            ))
        {
            Header = new PanelHeader("[bold]Quick Actions[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(MutedColor)
        };
        
        AnsiConsole.Write(quickPanel);
        AnsiConsole.WriteLine();
        
        // Main Categories Table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(MutedColor)
            .AddColumn(new TableColumn("[bold]#[/]").Centered())
            .AddColumn(new TableColumn("[bold]Category[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Description[/]").LeftAligned());
        
        table.AddRow("[cyan1]1[/]", "[cyan1]Market Data[/]", "Real-time quotes & historical data");
        table.AddRow("[cyan1]2[/]", "[dodgerblue2]Technical Analysis[/]", "TA indicators, patterns & charts");
        table.AddRow("[cyan1]3[/]", "[magenta1]Fundamental & News[/]", "Earnings, SEC filings & sentiment");
        table.AddRow("[cyan1]4[/]", "[yellow]Portfolio[/]", "Portfolio management & tracking");
        table.AddRow("[cyan1]5[/]", "[red]Risk Analysis[/]", "VaR, stress testing & optimization");
        table.AddRow("[cyan1]6[/]", "[green]Research[/]", "Academic papers & strategies");
        table.AddRow("[cyan1]7[/]", "[dodgerblue2]Advanced Analytics[/]", "ML, forecasting & statistics");
        table.AddRow("[cyan1]8[/]", "[magenta1]Trading Strategies[/]", "Strategy building & backtesting");
        table.AddRow("[cyan1]9[/]", "[red]Live Trading[/]", "Orders, execution & monitoring");
        table.AddRow("[cyan1]0[/]", "[grey]Settings & Help[/]", "Configuration & documentation");
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        
        var prompt = AnsiConsole.Prompt(
            new TextPrompt<string>("[cyan1]agent>[/]")
                .AllowEmpty()
        );
        
        return prompt.Trim();
    }
    
    /// <summary>
    /// Display a category submenu
    /// </summary>
    public static string ShowCategoryMenu(string category, Dictionary<string, string> commands)
    {
        ShowBanner();
        
        var panel = new Panel(new Markup($"[bold cyan1]{category}[/]"))
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(PrimaryColor),
            Padding = new Padding(2, 1)
        };
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        
        // Display commands in a table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(MutedColor)
            .AddColumn(new TableColumn("[bold]Command[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Description[/]").LeftAligned());
        
        foreach (var cmd in commands.OrderBy(c => c.Key))
        {
            table.AddRow($"[cyan1]{cmd.Key}[/]", cmd.Value);
        }
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Type command name or 'back' to return[/]");
        
        var prompt = AnsiConsole.Prompt(
            new TextPrompt<string>("[cyan1]agent>[/]")
                .AllowEmpty()
        );
        
        return prompt.Trim();
    }
    
    /// <summary>
    /// Display a section header
    /// </summary>
    public static void ShowSectionHeader(string title, string subtitle = null)
    {
        var rule = new Rule($"[bold cyan1]{title}[/]")
        {
            Style = new Style(PrimaryColor),
            Justification = Justify.Left
        };
        
        AnsiConsole.Write(rule);
        
        if (!string.IsNullOrEmpty(subtitle))
        {
            AnsiConsole.MarkupLine($"[dim]{subtitle}[/]");
        }
        
        AnsiConsole.WriteLine();
    }
    
    /// <summary>
    /// Display a section footer
    /// </summary>
    public static void ShowSectionFooter()
    {
        AnsiConsole.WriteLine();
        var rule = new Rule()
        {
            Style = new Style(MutedColor)
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }
    
    /// <summary>
    /// Display success message
    /// </summary>
    public static void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green][[OK]][/] {Markup.Escape(message)}");
    }
    
    /// <summary>
    /// Display error message
    /// </summary>
    public static void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red][[X]][/] {Markup.Escape(message)}");
    }
    
    /// <summary>
    /// Display warning message
    /// </summary>
    public static void ShowWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow][[!]][/] {Markup.Escape(message)}");
    }
    
    /// <summary>
    /// Display info message
    /// </summary>
    public static void ShowInfo(string message)
    {
        AnsiConsole.MarkupLine($"[cyan1][[i]][/] {Markup.Escape(message)}");
    }
    
    /// <summary>
    /// Create a data table
    /// </summary>
    public static Table CreateDataTable(params string[] columns)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(MutedColor);
        
        foreach (var column in columns)
        {
            table.AddColumn(new TableColumn($"[bold]{column}[/]"));
        }
        
        return table;
    }
    
    /// <summary>
    /// Display a progress bar for long operations
    /// </summary>
    public static async Task<T> WithProgressAsync<T>(string description, Func<Task<T>> action)
    {
        return await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            )
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask(description);
                task.IsIndeterminate = true;
                
                var result = await action();
                
                task.Value = 100;
                task.StopTask();
                
                return result;
            });
    }
    
    /// <summary>
    /// Display a spinner for operations
    /// </summary>
    public static async Task<T> WithSpinnerAsync<T>(string message, Func<Task<T>> action)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(new Style(PrimaryColor))
            .StartAsync(message, async ctx =>
            {
                return await action();
            });
    }
    
    /// <summary>
    /// Display data in a panel
    /// </summary>
    public static void ShowDataPanel(string title, string content, Color? borderColor = null)
    {
        var panel = new Panel(Markup.Escape(content))
        {
            Header = new PanelHeader($"[bold]{title}[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor ?? PrimaryColor),
            Padding = new Padding(2, 1)
        };
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }
    
    /// <summary>
    /// Prompt user for input with validation
    /// </summary>
    public static string PromptInput(string message, string defaultValue = null, bool allowEmpty = false)
    {
        var prompt = new TextPrompt<string>($"[cyan1]{message}[/]");
        
        if (!string.IsNullOrEmpty(defaultValue))
        {
            prompt.DefaultValue(defaultValue);
        }
        
        if (allowEmpty)
        {
            prompt.AllowEmpty();
        }
        
        return AnsiConsole.Prompt(prompt);
    }
    
    /// <summary>
    /// Prompt user to select from options
    /// </summary>
    public static string PromptSelection(string message, List<string> choices)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[cyan1]{message}[/]")
                .PageSize(10)
                .AddChoices(choices)
        );
    }
    
    /// <summary>
    /// Confirm action
    /// </summary>
    public static bool PromptConfirm(string message, bool defaultValue = true)
    {
        return AnsiConsole.Prompt(
            new ConfirmationPrompt($"[yellow]{message}[/]")
            {
                DefaultValue = defaultValue
            }
        );
    }
    
    /// <summary>
    /// Display a live updating status
    /// </summary>
    public static void ShowLiveStatus(Action<StatusContext> action)
    {
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(new Style(PrimaryColor))
            .Start("Processing...", action);
    }
    
    /// <summary>
    /// Create a tree view for hierarchical data
    /// </summary>
    public static Tree CreateTree(string rootName)
    {
        return new Tree(rootName)
            .Style(new Style(PrimaryColor));
    }
    
    /// <summary>
    /// Display command usage help
    /// </summary>
    public static void ShowCommandHelp(string command, string description, string usage, List<string> examples, List<string> relatedCommands = null)
    {
        ShowSectionHeader(command);
        
        // Description
        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(description)}[/]");
        AnsiConsole.WriteLine();
        
        // Usage
        var usagePanel = new Panel(Markup.Escape(usage))
        {
            Header = new PanelHeader("[bold]Usage[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(MutedColor)
        };
        AnsiConsole.Write(usagePanel);
        AnsiConsole.WriteLine();
        
        // Examples
        if (examples?.Count > 0)
        {
            var examplesMarkup = string.Join("\n", examples.Select(e => $"[cyan1]$[/] {Markup.Escape(e)}"));
            var examplesPanel = new Panel(examplesMarkup)
            {
                Header = new PanelHeader("[bold]Examples[/]", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(MutedColor)
            };
            AnsiConsole.Write(examplesPanel);
            AnsiConsole.WriteLine();
        }
        
        // Related Commands
        if (relatedCommands?.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Related Commands:[/]");
            foreach (var cmd in relatedCommands)
            {
                AnsiConsole.MarkupLine($"  [cyan1]{Markup.Escape(cmd)}[/]");
            }
            AnsiConsole.WriteLine();
        }
    }
    
    /// <summary>
    /// Create a bar chart for visualizing data
    /// Example usage:
    /// var data = new Dictionary&lt;string, double&gt; { {"AAPL", 45}, {"MSFT", 78}, {"GOOGL", 62} };
    /// var chart = UIHelper.CreateBarChart("Stock Returns (%)", data);
    /// AnsiConsole.Write(chart);
    /// </summary>
    public static BarChart CreateBarChart(string title, Dictionary<string, double> data, int maxValue = 100)
    {
        var chart = new BarChart()
            .Width(60)
            .Label($"[bold underline]{title}[/]")
            .CenterLabel();

        foreach (var item in data)
        {
            chart.AddItem(item.Key, item.Value, GetColorForValue(item.Value, maxValue));
        }

        return chart;
    }
    
    /// <summary>
    /// Create a breakdown chart (pie chart representation)
    /// </summary>
    public static BreakdownChart CreateBreakdownChart(string title, Dictionary<string, double> data)
    {
        var chart = new BreakdownChart()
            .Width(60)
            .UseValueFormatter(value => $"${value:N2}");

        var colors = new[] { Color.Cyan1, Color.Blue, Color.Magenta1, Color.Yellow, Color.Green, Color.Red, Color.Orange1, Color.Purple };
        int colorIndex = 0;

        foreach (var item in data)
        {
            chart.AddItem(item.Key, item.Value, colors[colorIndex % colors.Length]);
            colorIndex++;
        }

        return chart;
    }
    
    /// <summary>
    /// Display a simple ASCII line chart
    /// </summary>
    public static void ShowLineChart(string title, List<double> values, int width = 80, int height = 20, List<string>? labels = null)
    {
        if (values.Count == 0) return;
        
        ShowSectionHeader(title);
        
        var minValue = values.Min();
        var maxValue = values.Max();
        var range = maxValue - minValue;
        
        if (range == 0) range = 1;
        
        // Draw chart
        var canvas = new Canvas(width, height);
        
        for (int i = 0; i < values.Count - 1 && i < width; i++)
        {
            var y1 = (int)((values[i] - minValue) / range * (height - 1));
            var y2 = (int)((values[i + 1] - minValue) / range * (height - 1));
            
            // Draw line from point to point
            var x1 = (int)((double)i / values.Count * width);
            var x2 = (int)((double)(i + 1) / values.Count * width);
            
            canvas.SetPixel(x1, height - 1 - y1, Color.Cyan1);
            
            if (Math.Abs(y2 - y1) > 1)
            {
                var steps = Math.Abs(y2 - y1);
                for (int step = 0; step <= steps; step++)
                {
                    var interpY = y1 + (y2 - y1) * step / steps;
                    canvas.SetPixel(x1, height - 1 - (int)interpY, Color.Cyan1);
                }
            }
        }
        
        AnsiConsole.Write(canvas);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Min: {minValue:F2} | Max: {maxValue:F2} | Range: {range:F2}[/]");
        AnsiConsole.WriteLine();
    }
    
    /// <summary>
    /// Display sparkline (mini inline chart)
    /// </summary>
    public static string CreateSparkline(List<double> values)
    {
        if (values.Count == 0) return "";
        
        var bars = new[] { "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█" };
        var min = values.Min();
        var max = values.Max();
        var range = max - min;
        
        if (range == 0) return new string('▄', values.Count);
        
        var result = "";
        foreach (var value in values)
        {
            var normalized = (value - min) / range;
            var index = (int)(normalized * (bars.Length - 1));
            result += bars[index];
        }
        
        return result;
    }
    
    /// <summary>
    /// Create a comparison table with sparklines
    /// </summary>
    public static Table CreateComparisonTable(Dictionary<string, List<double>> data, string column1 = "Symbol", string column2 = "Trend")
    {
        var table = CreateDataTable(column1, column2, "Change %");
        
        foreach (var item in data)
        {
            var sparkline = CreateSparkline(item.Value);
            var change = item.Value.Count > 1 
                ? ((item.Value.Last() - item.Value.First()) / item.Value.First() * 100)
                : 0;
            
            var changeColor = change >= 0 ? "green" : "red";
            table.AddRow(
                $"[cyan1]{item.Key}[/]", 
                $"[dim]{sparkline}[/]",
                $"[{changeColor}]{change:+0.00;-0.00}%[/]"
            );
        }
        
        return table;
    }
    
    private static Color GetColorForValue(double value, double maxValue)
    {
        var percentage = maxValue > 0 ? value / maxValue : 0;
        
        if (percentage >= 0.8) return Color.Green;
        if (percentage >= 0.6) return Color.Yellow;
        if (percentage >= 0.4) return Color.Orange1;
        return Color.Red;
    }
}
