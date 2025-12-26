using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace QuantResearchAgent.Services;

/// <summary>
/// Research notebook environment service for interactive research workflows
/// </summary>
public class NotebookService
{
    private readonly ILogger<NotebookService> _logger;

    public NotebookService(ILogger<NotebookService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Notebook cell types
    /// </summary>
    public enum CellType
    {
        Code,
        Markdown,
        Raw
    }

    /// <summary>
    /// Notebook execution status
    /// </summary>
    public enum ExecutionStatus
    {
        Idle,
        Running,
        Completed,
        Failed
    }

    /// <summary>
    /// Notebook cell definition
    /// </summary>
    public class NotebookCell
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public CellType Type { get; set; } = CellType.Code;
        public string Source { get; set; } = string.Empty;
        public Dictionary<string, object> Outputs { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public ExecutionStatus Status { get; set; } = ExecutionStatus.Idle;
        public DateTime? ExecutionTime { get; set; }
        public int ExecutionCount { get; set; }
    }

    /// <summary>
    /// Research notebook definition
    /// </summary>
    public class ResearchNotebook
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<NotebookCell> Cells { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime Modified { get; set; } = DateTime.UtcNow;
        public string KernelName { get; set; } = "csharp";
        public string Language { get; set; } = "C#";
    }

    /// <summary>
    /// Cell execution result
    /// </summary>
    public class CellExecutionResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public TimeSpan ExecutionTime { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Create a new research notebook
    /// </summary>
    public ResearchNotebook CreateNotebook(string name, string description = "")
    {
        _logger.LogInformation("Creating new research notebook: {Name}", name);

        var notebook = new ResearchNotebook
        {
            Name = name,
            Description = description
        };

        // Add a welcome cell
        var welcomeCell = new NotebookCell
        {
            Type = CellType.Markdown,
            Source = $"# {name}\n\n{description}\n\n*Created on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*"
        };

        notebook.Cells.Add(welcomeCell);

        return notebook;
    }

    /// <summary>
    /// Add a cell to a notebook
    /// </summary>
    public void AddCell(ResearchNotebook notebook, CellType type, string source, int? index = null)
    {
        var cell = new NotebookCell
        {
            Type = type,
            Source = source
        };

        if (index.HasValue && index.Value >= 0 && index.Value <= notebook.Cells.Count)
        {
            notebook.Cells.Insert(index.Value, cell);
        }
        else
        {
            notebook.Cells.Add(cell);
        }

        notebook.Modified = DateTime.UtcNow;

        _logger.LogInformation("Added {Type} cell to notebook {NotebookName}", type, notebook.Name);
    }

    /// <summary>
    /// Remove a cell from a notebook
    /// </summary>
    public void RemoveCell(ResearchNotebook notebook, string cellId)
    {
        var cell = notebook.Cells.FirstOrDefault(c => c.Id == cellId);
        if (cell != null)
        {
            notebook.Cells.Remove(cell);
            notebook.Modified = DateTime.UtcNow;

            _logger.LogInformation("Removed cell from notebook {NotebookName}", notebook.Name);
        }
    }

    /// <summary>
    /// Update cell source
    /// </summary>
    public void UpdateCellSource(ResearchNotebook notebook, string cellId, string newSource)
    {
        var cell = notebook.Cells.FirstOrDefault(c => c.Id == cellId);
        if (cell != null)
        {
            cell.Source = newSource;
            notebook.Modified = DateTime.UtcNow;

            _logger.LogInformation("Updated cell source in notebook {NotebookName}", notebook.Name);
        }
    }

    /// <summary>
    /// Execute a single cell
    /// </summary>
    public async Task<CellExecutionResult> ExecuteCell(NotebookCell cell)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            cell.Status = ExecutionStatus.Running;
            cell.ExecutionTime = DateTime.UtcNow;
            cell.ExecutionCount++;

            _logger.LogInformation("Executing cell {CellId} of type {CellType}", cell.Id, cell.Type);

            CellExecutionResult result;

            switch (cell.Type)
            {
                case CellType.Code:
                    result = await ExecuteCodeCell(cell);
                    break;
                case CellType.Markdown:
                    result = await ExecuteMarkdownCell(cell);
                    break;
                case CellType.Raw:
                    result = new CellExecutionResult { Success = true, Output = cell.Source };
                    break;
                default:
                    result = new CellExecutionResult
                    {
                        Success = false,
                        Error = $"Unknown cell type: {cell.Type}"
                    };
                    break;
            }

            cell.Status = result.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed;
            result.ExecutionTime = stopwatch.Elapsed;

            // Store results in cell
            cell.Outputs["execution_result"] = result;

            return result;
        }
        catch (Exception ex)
        {
            cell.Status = ExecutionStatus.Failed;

            _logger.LogError(ex, "Error executing cell {CellId}", cell.Id);

            return new CellExecutionResult
            {
                Success = false,
                Error = ex.Message,
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Execute all cells in a notebook
    /// </summary>
    public async Task<List<CellExecutionResult>> ExecuteNotebook(ResearchNotebook notebook)
    {
        _logger.LogInformation("Executing notebook {NotebookName} with {CellCount} cells",
            notebook.Name, notebook.Cells.Count);

        var results = new List<CellExecutionResult>();

        foreach (var cell in notebook.Cells.Where(c => c.Type == CellType.Code))
        {
            var result = await ExecuteCell(cell);
            results.Add(result);

            // Stop execution on first error
            if (!result.Success)
            {
                _logger.LogWarning("Notebook execution stopped due to error in cell {CellId}", cell.Id);
                break;
            }
        }

        notebook.Modified = DateTime.UtcNow;

        _logger.LogInformation("Notebook execution completed. {SuccessCount}/{TotalCount} cells executed successfully",
            results.Count(r => r.Success), results.Count);

        return results;
    }

    /// <summary>
    /// Export notebook to Jupyter format
    /// </summary>
    public string ExportToJupyterFormat(ResearchNotebook notebook)
    {
        var jupyterNotebook = new
        {
            cells = notebook.Cells.Select(cell => new
            {
                cell_type = cell.Type.ToString().ToLower(),
                source = cell.Source.Split('\n').ToArray(),
                outputs = cell.Outputs.ContainsKey("execution_result") ?
                    new[] { new
                    {
                        output_type = "stream",
                        name = "stdout",
                        text = new[] { ((CellExecutionResult)cell.Outputs["execution_result"]).Output }
                    }} : Array.Empty<object>(),
                metadata = cell.Metadata,
                execution_count = cell.ExecutionCount
            }).ToArray(),
            metadata = new
            {
                kernelspec = new
                {
                    display_name = notebook.Language,
                    language = notebook.Language.ToLower(),
                    name = notebook.KernelName
                },
                language_info = new
                {
                    name = notebook.Language.ToLower(),
                    version = "11.0"
                }
            },
            nbformat = 4,
            nbformat_minor = 2
        };

        return JsonSerializer.Serialize(jupyterNotebook, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Import notebook from Jupyter format
    /// </summary>
    public ResearchNotebook ImportFromJupyterFormat(string jupyterJson)
    {
        try
        {
            var jupyterNotebook = JsonSerializer.Deserialize<Dictionary<string, object>>(jupyterJson);
            if (jupyterNotebook == null || !jupyterNotebook.ContainsKey("cells"))
            {
                throw new ArgumentException("Invalid Jupyter notebook format");
            }

            var notebook = CreateNotebook("Imported Notebook", "Imported from Jupyter");

            // Parse cells
            var cellsJson = JsonSerializer.Serialize(jupyterNotebook["cells"]);
            var cells = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cellsJson);

            if (cells != null)
            {
                foreach (var cellJson in cells)
                {
                    var cellType = cellJson.ContainsKey("cell_type") ?
                        Enum.Parse<CellType>((string)cellJson["cell_type"], true) : CellType.Code;

                    var sourceArray = cellJson.ContainsKey("source") ?
                        JsonSerializer.Deserialize<string[]>((string)cellJson["source"]) : Array.Empty<string>();

                    var source = string.Join("\n", sourceArray ?? Array.Empty<string>());

                    AddCell(notebook, cellType, source);
                }
            }

            return notebook;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Jupyter notebook");
            throw;
        }
    }

    /// <summary>
    /// Create a research template notebook
    /// </summary>
    public ResearchNotebook CreateResearchTemplate(string researchTopic)
    {
        var notebook = CreateNotebook($"{researchTopic} Research", $"Research notebook for {researchTopic}");

        // Add research methodology section
        AddCell(notebook, CellType.Markdown,
            $"## Research Methodology\n\n" +
            $"### Objective\n" +
            $"Analyze {researchTopic} using quantitative methods\n\n" +
            $"### Data Sources\n" +
            $"- Financial market data\n" +
            $"- Economic indicators\n" +
            $"- Alternative data sources\n\n" +
            $"### Analysis Framework\n" +
            $"- Statistical testing\n" +
            $"- Time series analysis\n" +
            $"- Machine learning models");

        // Add data loading section
        AddCell(notebook, CellType.Code,
            $"// Load required data for {researchTopic} analysis\n" +
            "using QuantResearchAgent.Services;\n" +
            "using System.Collections.Generic;\n" +
            "using System.Linq;\n\n" +
            "// Initialize data services\n" +
            "var marketData = new List<double>(); // Load your market data here\n" +
            "var dates = new List<DateTime>(); // Load corresponding dates\n\n" +
            "Console.WriteLine($\"Loaded {{marketData.Count}} data points\");");

        // Add exploratory analysis section
        AddCell(notebook, CellType.Code,
            "// Exploratory Data Analysis\n" +
            "using MathNet.Numerics.Statistics;\n\n" +
            "if (marketData.Any())\n" +
            "{\n" +
            "    var mean = marketData.Mean();\n" +
            "    var std = marketData.StandardDeviation();\n" +
            "    var min = marketData.Min();\n" +
            "    var max = marketData.Max();\n\n" +
            "    Console.WriteLine($\"Mean: {mean:F4}\");\n" +
            "    Console.WriteLine($\"Std Dev: {std:F4}\");\n" +
            "    Console.WriteLine($\"Range: {min:F4} - {max:F4}\");\n" +
            "}\n" +
            "else\n" +
            "{\n" +
            "    Console.WriteLine(\"No data available for analysis\");\n" +
            "}");

        // Add statistical testing section
        AddCell(notebook, CellType.Code,
            "// Statistical Analysis\n" +
            "using QuantResearchAgent.Services;\n\n" +
            "// Perform stationarity tests\n" +
            "var adfTest = new StatisticalTestingService(null);\n" +
            "// Note: This would require proper service initialization\n\n" +
            "Console.WriteLine(\"Statistical analysis section - implement specific tests\");");

        // Add modeling section
        AddCell(notebook, CellType.Code,
            "// Modeling and Forecasting\n" +
            "using QuantResearchAgent.Services;\n\n" +
            "// Implement your modeling approach here\n" +
            "// Options: ARIMA, GARCH, Machine Learning models\n\n" +
            "Console.WriteLine(\"Modeling section - implement your analysis\");");

        // Add results and conclusions
        AddCell(notebook, CellType.Markdown,
            "## Results and Conclusions\n\n" +
            "### Key Findings\n" +
            "- Finding 1\n" +
            "- Finding 2\n" +
            "- Finding 3\n\n" +
            "### Implications\n" +
            "- Implication 1\n" +
            "- Implication 2\n\n" +
            "### Future Research\n" +
            "- Area 1 for further investigation\n" +
            "- Area 2 for further investigation");

        _logger.LogInformation("Created research template notebook for topic: {Topic}", researchTopic);

        return notebook;
    }

    /// <summary>
    /// Get available notebook templates
    /// </summary>
    public List<ResearchNotebook> GetNotebookTemplates()
    {
        return new List<ResearchNotebook>
        {
            CreateResearchTemplate("Portfolio Optimization"),
            CreateResearchTemplate("Risk Management"),
            CreateResearchTemplate("Market Microstructure"),
            CreateResearchTemplate("Algorithmic Trading"),
            CreateResearchTemplate("Factor Investing"),
            CreateResearchTemplate("Sentiment Analysis")
        };
    }

    /// <summary>
    /// Generate notebook summary
    /// </summary>
    public Dictionary<string, object> GenerateNotebookSummary(ResearchNotebook notebook)
    {
        var summary = new Dictionary<string, object>
        {
            ["id"] = notebook.Id,
            ["name"] = notebook.Name,
            ["description"] = notebook.Description,
            ["created"] = notebook.Created,
            ["modified"] = notebook.Modified,
            ["totalCells"] = notebook.Cells.Count,
            ["codeCells"] = notebook.Cells.Count(c => c.Type == CellType.Code),
            ["markdownCells"] = notebook.Cells.Count(c => c.Type == CellType.Markdown),
            ["executedCells"] = notebook.Cells.Count(c => c.ExecutionCount > 0),
            ["failedCells"] = notebook.Cells.Count(c => c.Status == ExecutionStatus.Failed),
            ["kernel"] = notebook.KernelName,
            ["language"] = notebook.Language
        };

        return summary;
    }

    private async Task<CellExecutionResult> ExecuteCodeCell(NotebookCell cell)
    {
        try
        {
            // For now, we'll simulate code execution
            // In a real implementation, this would interface with a code execution engine

            var source = cell.Source.Trim();

            if (string.IsNullOrEmpty(source))
            {
                return new CellExecutionResult
                {
                    Success = true,
                    Output = ""
                };
            }

            // Simple code analysis and execution simulation
            if (source.Contains("Console.WriteLine"))
            {
                // Extract the content inside WriteLine
                var startIndex = source.IndexOf("Console.WriteLine(") + 18;
                var endIndex = source.LastIndexOf(")");
                if (startIndex > 17 && endIndex > startIndex)
                {
                    var content = source.Substring(startIndex, endIndex - startIndex);
                    // Remove quotes if present
                    if (content.StartsWith("\"") && content.EndsWith("\""))
                    {
                        content = content.Substring(1, content.Length - 2);
                    }

                    return new CellExecutionResult
                    {
                        Success = true,
                        Output = content
                    };
                }
            }

            // Simulate variable assignments and calculations
            if (source.Contains("=") && source.Contains("var "))
            {
                return new CellExecutionResult
                {
                    Success = true,
                    Output = "Variable assigned successfully"
                };
            }

            // Default successful execution
            return new CellExecutionResult
            {
                Success = true,
                Output = $"Executed: {source.Length} characters of code"
            };
        }
        catch (Exception ex)
        {
            return new CellExecutionResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<CellExecutionResult> ExecuteMarkdownCell(NotebookCell cell)
    {
        // Markdown cells don't need execution, just rendering
        return new CellExecutionResult
        {
            Success = true,
            Output = "Markdown rendered successfully"
        };
    }
}