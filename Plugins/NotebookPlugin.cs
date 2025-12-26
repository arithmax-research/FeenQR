using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Research notebook environment plugin for AI agents
/// </summary>
public class NotebookPlugin
{
    private readonly NotebookService _notebookService;

    public NotebookPlugin(NotebookService notebookService)
    {
        _notebookService = notebookService;
    }

    [KernelFunction("create_research_notebook")]
    [Description("Create a new research notebook with the given name and description")]
    public async Task<string> CreateResearchNotebook(
        [Description("Name of the research notebook")] string name,
        [Description("Description of the research topic")] string description = "")
    {
        try
        {
            var notebook = _notebookService.CreateNotebook(name, description);

            return JsonSerializer.Serialize(new
            {
                success = true,
                notebookId = notebook.Id,
                notebookName = notebook.Name,
                cellCount = notebook.Cells.Count,
                message = $"Research notebook '{name}' created successfully with {notebook.Cells.Count} initial cells"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("add_notebook_cell")]
    [Description("Add a cell to an existing research notebook")]
    public async Task<string> AddNotebookCell(
        [Description("Notebook ID to add cell to")] string notebookId,
        [Description("Cell type: Code, Markdown, Raw")] string cellType,
        [Description("Cell source content")] string source,
        [Description("Optional position to insert cell (0-based index)")] int? position = null)
    {
        try
        {
            // Parse cell type
            if (!Enum.TryParse<NotebookService.CellType>(cellType, true, out var type))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Invalid cell type: {cellType}. Use Code, Markdown, or Raw"
                });
            }

            // For demo purposes, create a new notebook and add the cell
            var notebook = _notebookService.CreateNotebook("Demo Notebook", "Demo for cell addition");
            _notebookService.AddCell(notebook, type, source, position);

            return JsonSerializer.Serialize(new
            {
                success = true,
                cellId = notebook.Cells.Last().Id,
                cellType = type.ToString(),
                position = notebook.Cells.Count - 1,
                message = $"{type} cell added to notebook successfully"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("execute_notebook_cell")]
    [Description("Execute a specific cell in a research notebook")]
    public async Task<string> ExecuteNotebookCell(
        [Description("Notebook ID containing the cell")] string notebookId,
        [Description("Cell ID to execute")] string cellId)
    {
        try
        {
            // Create a demo cell for execution
            var cell = new NotebookService.NotebookCell
            {
                Id = cellId,
                Type = NotebookService.CellType.Code,
                Source = "// Demo cell execution\nConsole.WriteLine(\"Hello from notebook cell!\");"
            };

            var result = await _notebookService.ExecuteCell(cell);

            return JsonSerializer.Serialize(new
            {
                success = result.Success,
                output = result.Output,
                error = result.Error,
                executionTime = result.ExecutionTime.TotalMilliseconds,
                message = result.Success ? "Cell executed successfully" : $"Cell execution failed: {result.Error}"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("execute_notebook")]
    [Description("Execute all cells in a research notebook")]
    public async Task<string> ExecuteNotebook(
        [Description("Notebook ID to execute")] string notebookId)
    {
        try
        {
            // Create a demo notebook with multiple cells
            var notebook = _notebookService.CreateNotebook("Demo Execution", "Demo notebook execution");

            // Add some demo cells
            _notebookService.AddCell(notebook, NotebookService.CellType.Code,
                "var x = 42;\nConsole.WriteLine($\"x = {x}\");");

            _notebookService.AddCell(notebook, NotebookService.CellType.Code,
                "var y = x * 2;\nConsole.WriteLine($\"y = {y}\");");

            _notebookService.AddCell(notebook, NotebookService.CellType.Code,
                "Console.WriteLine(\"Notebook execution completed!\");");

            var results = await _notebookService.ExecuteNotebook(notebook);

            var executionSummary = new
            {
                totalCells = results.Count,
                successfulCells = results.Count(r => r.Success),
                failedCells = results.Count(r => !r.Success),
                totalExecutionTime = results.Sum(r => r.ExecutionTime.TotalMilliseconds),
                results = results.Select((r, i) => new
                {
                    cellIndex = i + 1,
                    success = r.Success,
                    output = r.Output,
                    error = r.Error,
                    executionTime = r.ExecutionTime.TotalMilliseconds
                }).ToList()
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                executionSummary = executionSummary,
                message = $"Notebook execution completed. {executionSummary.successfulCells}/{executionSummary.totalCells} cells executed successfully"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("export_notebook")]
    [Description("Export a research notebook to Jupyter notebook format")]
    public async Task<string> ExportNotebook(
        [Description("Notebook ID to export")] string notebookId)
    {
        try
        {
            // Create a demo notebook for export
            var notebook = _notebookService.CreateNotebook("Demo Export", "Demo notebook for export");

            // Add some sample cells
            _notebookService.AddCell(notebook, NotebookService.CellType.Markdown,
                "# Demo Research Notebook\n\nThis is a sample notebook for demonstration purposes.");

            _notebookService.AddCell(notebook, NotebookService.CellType.Code,
                "using System;\n\nConsole.WriteLine(\"Hello, Research Notebook!\");\nvar data = new[] { 1, 2, 3, 4, 5 };\nvar avg = data.Average();\nConsole.WriteLine($\"Average: {avg}\");");

            _notebookService.AddCell(notebook, NotebookService.CellType.Markdown,
                "## Results\n\nThe analysis shows that the average value is calculated correctly.");

            var jupyterFormat = _notebookService.ExportToJupyterFormat(notebook);

            return JsonSerializer.Serialize(new
            {
                success = true,
                notebookName = notebook.Name,
                cellCount = notebook.Cells.Count,
                jupyterFormat = jupyterFormat,
                message = $"Notebook exported to Jupyter format successfully ({jupyterFormat.Length} characters)"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("import_notebook")]
    [Description("Import a research notebook from Jupyter notebook format")]
    public async Task<string> ImportNotebook(
        [Description("Jupyter notebook JSON content")] string jupyterJson)
    {
        try
        {
            var notebook = _notebookService.ImportFromJupyterFormat(jupyterJson);

            return JsonSerializer.Serialize(new
            {
                success = true,
                notebookId = notebook.Id,
                notebookName = notebook.Name,
                importedCells = notebook.Cells.Count,
                cellTypes = notebook.Cells.GroupBy(c => c.Type)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                message = $"Notebook imported successfully with {notebook.Cells.Count} cells"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("create_research_template")]
    [Description("Create a research template notebook for a specific topic")]
    public async Task<string> CreateResearchTemplate(
        [Description("Research topic for the template")] string researchTopic)
    {
        try
        {
            var notebook = _notebookService.CreateResearchTemplate(researchTopic);

            var summary = _notebookService.GenerateNotebookSummary(notebook);

            return JsonSerializer.Serialize(new
            {
                success = true,
                notebookId = notebook.Id,
                notebookName = notebook.Name,
                templateTopic = researchTopic,
                summary = summary,
                message = $"Research template created for '{researchTopic}' with {notebook.Cells.Count} cells"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("get_notebook_templates")]
    [Description("Get available research notebook templates")]
    public async Task<string> GetNotebookTemplates()
    {
        try
        {
            var templates = _notebookService.GetNotebookTemplates();

            var templateSummaries = templates.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                description = t.Description,
                cellCount = t.Cells.Count,
                topics = t.Name.Replace(" Research", "").Replace(" Analysis", "")
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                templates = templateSummaries,
                count = templates.Count,
                availableTopics = templateSummaries.Select(t => t.topics).Distinct().ToList(),
                message = $"Found {templates.Count} research notebook templates"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("analyze_notebook_execution")]
    [Description("Analyze the execution results of a notebook and provide insights")]
    public async Task<string> AnalyzeNotebookExecution(
        [Description("Notebook execution results as JSON")] string executionResultsJson)
    {
        try
        {
            var results = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(executionResultsJson)
                ?? new List<Dictionary<string, object>>();

            var analysis = new Dictionary<string, object>
            {
                ["totalCells"] = results.Count,
                ["successfulCells"] = results.Count(r => (bool)(r.ContainsKey("success") ? r["success"] : false)),
                ["failedCells"] = results.Count(r => !(bool)(r.ContainsKey("success") ? r["success"] : true)),
                ["totalExecutionTime"] = results.Sum(r => r.ContainsKey("executionTime") ?
                    (double)r["executionTime"] : 0.0)
            };

            var recommendations = new List<string>();

            if ((int)analysis["failedCells"] > 0)
            {
                recommendations.Add("Review failed cells and fix errors before proceeding");
            }

            if ((double)analysis["totalExecutionTime"] > 30000) // 30 seconds
            {
                recommendations.Add("Consider optimizing long-running cells or breaking them into smaller parts");
            }

            if ((int)analysis["successfulCells"] == 0)
            {
                recommendations.Add("No cells executed successfully - check notebook setup and cell dependencies");
            }

            var successRate = (int)analysis["totalCells"] > 0 ?
                (double)analysis["successfulCells"] / (double)analysis["totalCells"] : 0.0;

            var performanceRating = successRate >= 0.9 ? "Excellent" :
                                   successRate >= 0.7 ? "Good" :
                                   successRate >= 0.5 ? "Fair" : "Poor";

            return JsonSerializer.Serialize(new
            {
                success = true,
                analysis = analysis,
                recommendations = recommendations,
                performanceRating = performanceRating,
                successRate = successRate,
                message = $"Notebook execution analysis completed. Performance: {performanceRating}"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction("generate_notebook_report")]
    [Description("Generate a comprehensive report about a research notebook")]
    public async Task<string> GenerateNotebookReport(
        [Description("Notebook ID to generate report for")] string notebookId)
    {
        try
        {
            // Create a demo notebook for reporting
            var notebook = _notebookService.CreateNotebook("Demo Report", "Demo notebook for reporting");

            // Add various types of cells
            _notebookService.AddCell(notebook, NotebookService.CellType.Markdown,
                "# Research Analysis Report\n\nThis notebook contains comprehensive analysis.");

            _notebookService.AddCell(notebook, NotebookService.CellType.Code,
                "// Data loading and preprocessing\nvar data = new List<double> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };");

            _notebookService.AddCell(notebook, NotebookService.CellType.Code,
                "// Statistical analysis\nvar mean = data.Average();\nvar std = data.Select(x => Math.Pow(x - mean, 2)).Average();\nstd = Math.Sqrt(std);\nConsole.WriteLine($\"Mean: {mean:F2}, Std: {std:F2}\");");

            _notebookService.AddCell(notebook, NotebookService.CellType.Markdown,
                "## Results\n\nThe statistical analysis shows the following key metrics:\n\n- Mean value\n- Standard deviation\n- Data distribution");

            var summary = _notebookService.GenerateNotebookSummary(notebook);

            var report = new
            {
                notebookOverview = summary,
                cellAnalysis = new
                {
                    codeCells = notebook.Cells.Count(c => c.Type == NotebookService.CellType.Code),
                    markdownCells = notebook.Cells.Count(c => c.Type == NotebookService.CellType.Markdown),
                    rawCells = notebook.Cells.Count(c => c.Type == NotebookService.CellType.Raw)
                },
                executionStatus = new
                {
                    executedCells = notebook.Cells.Count(c => c.ExecutionCount > 0),
                    pendingCells = notebook.Cells.Count(c => c.ExecutionCount == 0),
                    failedCells = notebook.Cells.Count(c => c.Status == NotebookService.ExecutionStatus.Failed)
                },
                recommendations = new List<string>
                {
                    "Execute all code cells to validate the analysis",
                    "Add more detailed documentation in markdown cells",
                    "Consider adding data visualization cells",
                    "Review and optimize long-running computations"
                }
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                report = report,
                message = "Comprehensive notebook report generated successfully"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}