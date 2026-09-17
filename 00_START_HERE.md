# BuilderCore — Start Here

This package is intended to be copied into the root of a newly created Git repository before giving the implementation prompt to a coding agent.

## Recommended sequence

1. Create an empty GitHub repository named `BuilderCore`.
2. Clone it locally:
   ```bash
   git clone <repository-url>
   cd BuilderCore
   ```
3. Extract/copy the complete contents of this package into the repository root.
4. Confirm `.NET 10`:
   ```bash
   dotnet --version
   ```
5. Commit the specification baseline:
   ```bash
   git add .
   git commit -m "Add BuilderCore implementation specification"
   git push
   ```
6. Open the repository folder in Cursor, VS Code/Copilot, or another coding-agent environment.
7. Give the agent the contents of `AGENT_LAUNCH_PROMPT.md` or simply tell it to execute that file.
8. Allow the agent to create the solution and application. Do not manually scaffold the .NET projects unless the agent is blocked.
9. When the agent reports completion, independently run:
   ```bash
   dotnet restore
   dotnet build
   dotnet test
   dotnet run --project src/BuilderCore.Web
   ```
10. Review the demo workflow and then commit the implementation.

## No infrastructure required initially

The local POC must work without:
- SQL Server
- Azure Entra ID
- AWS CLI
- Docker
- an external database

The initial runtime uses:
- .NET 10
- Blazor Interactive Server
- Dapper
- Microsoft.Data.Sqlite
- local `buildercore.db`
- safe Development-only local authentication
- realistic fictional demo data

Azure Entra ID and AWS deployment are later steps.

## Expected repository after implementation

```text
BuilderCore/
├── BuilderCore.sln
├── src/
│   └── BuilderCore.Web/
├── tests/
│   └── BuilderCore.Tests/
├── database/
├── docs/
├── mockups/
├── .github/
├── 00_START_HERE.md
├── AGENT_LAUNCH_PROMPT.md
├── ONE_PROMPT_BUILD.md
├── README.md
└── IMPLEMENTATION_NOTES.md
```
