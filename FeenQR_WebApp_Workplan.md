# FeenQR Web Application Enhancement Workplan

## Overview
This workplan outlines the transformation of FeenQR from a terminal-based CLI into a modern web application using ASP.NET Core with Blazor WebAssembly. The goal is to improve usability by providing a visual, interactive interface while reusing existing .NET core logic. The webapp will mimic the aesthetics of OpenBB (open-source financial terminal) or Bloomberg Terminal: professional dark themes, data-dense grids, real-time tickers, modular dashboards, and color-coded indicators (e.g., green for gains, red for losses).

**Key Objectives**:
- Maintain 97+ CLI commands as web features.
- Achieve 80% user satisfaction through intuitive UX.
- Deliver MVP in 3 months (200-300 hours).
- Target quant researchers, analysts, and traders.

**Tech Stack**:
- Frontend: Blazor WebAssembly + MudBlazor (for Bloomberg-like components).
- Backend: ASP.NET Core API.
- Shared: .NET 8.0 class libraries for core logic.
- Hosting: Azure App Service or Vercel.
- Tools: VS Code, Git, Figma (for wireframes).
- **Project Structure**: Webapp located in sibling directory `../FeenQR.WebApp` to avoid CLI build conflicts.

**Aesthetics Guidelines** (Mimicking OpenBB/Bloomberg)**:
- **Color Scheme**: Dark background (#1a1a1a), green (#00ff00) for positive, red (#ff0000) for negative, blue accents (#007bff).
- **Layout**: Modular dashboard with resizable panels, sidebar navigation, top ticker bar.
- **Components**: Data tables with sorting/filtering, interactive charts (candlestick, line), real-time updates with blinking indicators.
- **Typography**: Monospace fonts for data (e.g., Roboto Mono), clean sans-serif for UI.
- **Interactivity**: Hover tooltips, drag-and-drop for portfolios, keyboard shortcuts.
- **Responsiveness**: Desktop-first, tablet/mobile support with collapsible menus.

## Phase 1: Planning & Setup (Week 1-2, Effort: 20-30 hours)
**Goal**: Define requirements and architecture.

### Milestone 1.1: Requirements Gathering (Days 1-3)
- Analyze CLI workflows and map to web features.
- User research and wireframes (Figma).
- Define MVP: Market Data, Technical Analysis, Portfolio.

### Milestone 1.2: Architecture & Tech Stack (Days 4-7)
- Refactor core logic into `FeenQR.Core`.
- Set up Blazor WASM + API projects.
- Plan authentication and security.

### Milestone 1.3: Environment Setup (Days 8-10)
- Scaffold projects with `dotnet new`.
- Configure development tools.
- **Launch**: Use `./launch.sh web` from CLI directory to start webapp at http://localhost:5157.

**Files Needed**:
- `FeenQR.WebApp.Client.csproj` (Blazor WASM).
- `FeenQR.WebApp.Server.csproj` (API).
- `FeenQR.Core.csproj` (shared library).
- `appsettings.json` (config).

**Libraries**:
- `Microsoft.AspNetCore.Components.WebAssembly` (Blazor).
- `MudBlazor` (UI components).
- `Microsoft.AspNetCore.SignalR` (real-time updates).

## Phase 2: Core Development (Weeks 3-8, Effort: 100-150 hours)
**Goal**: Build UI and integrate features.

### Milestone 2.1: Authentication & Navigation (Weeks 3-4)
- Implement login and sidebar navigation.

### Milestone 2.2: Market Data Module (Weeks 5-6)
- Search forms, data tables, charts.

### Milestone 2.3: Analysis & Portfolio Modules (Weeks 7-8)
- Interactive charts, sentiment analysis, portfolio dashboards.

**Files Needed** (per module):
- `Pages/MarketData.razor` (component).
- `Services/MarketDataApiService.cs` (API client).
- `Models/QuoteModel.cs` (data models).

**Libraries**:
- `BlazorChart` or `Plotly.Blazor` (charts).
- `Microsoft.AspNetCore.Components.Authorization` (auth).

## Phase 3: Advanced Features & Polish (Weeks 9-10, Effort: 40-60 hours)
**Goal**: Enhance UX.

### Milestone 3.1: UX Enhancements (Week 9)
- Themes, responsiveness, accessibility.

### Milestone 3.2: Integration & Optimization (Week 10)
- Full features, performance tuning.

**Files Needed**:
- `wwwroot/css/site.css` (custom styles).
- `Shared/MainLayout.razor` (layout).

**Libraries**:
- `Microsoft.AspNetCore.Components.Web` (PWA support).

## Phase 4: Testing & Validation (Weeks 11-12, Effort: 20-30 hours)
**Goal**: Ensure quality.

### Milestone 4.1: Testing (Week 11)
- Unit/integration tests, UX sessions.

### Milestone 4.2: Validation (Week 12)
- Beta testing, security audit.

**Libraries**:
- `xUnit` (testing).
- `Selenium` (UI testing).

## Phase 5: Deployment & Launch (Week 13, Effort: 10-20 hours)
**Goal**: Go live.

### Milestone 5.1: Deployment Setup
- CI/CD with GitHub Actions.

### Milestone 5.2: Launch & Monitoring
- Hosting and analytics.

**Files Needed**:
- `.github/workflows/deploy.yml` (CI/CD).
- `README.md` (updated docs).

## Phase 6: Maintenance & Iteration (Ongoing)
- Bug fixes, feature updates.

## Additional Efforts
- **Training**: Team training on Blazor (2-4 hours).
- **Documentation**: API docs with Swagger.
- **Legal/Compliance**: Ensure data privacy (GDPR/CCPA).
- **Marketing**: Demo videos showcasing Bloomberg-like UI.
- **Risk Mitigation**: Weekly stand-ups, version control with Git branches.

## Success Metrics
- 80% user satisfaction.
- <2s load times.
- 1000+ users in 6 months.

For implementation, start with scaffolding the project. Contact for code samples!