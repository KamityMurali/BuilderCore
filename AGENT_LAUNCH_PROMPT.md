# Agent Launch Prompt

Read `ONE_PROMPT_BUILD.md` in the repository root and treat it as the authoritative implementation instruction.

Before writing code, read every supporting specification, database, mockup, testing, security, and demo-data file referenced by it.

Implement the complete BuilderPOC application directly in this repository.

Work autonomously through the full implementation. Do not stop after producing a plan, architecture description, scaffolding, or partial implementation. Do not ask for confirmation between implementation phases unless a genuine external dependency makes progress impossible.

The application must be runnable locally without AWS Cognito, SQL Server, Docker, or other external infrastructure. Use the Development-only authentication mechanism, Dapper, Microsoft.Data.Sqlite, automatic local database initialization, and realistic demo data specified in the package.

Create the solution/projects if they do not already exist.

Continuously compile and test as you implement. Before declaring completion, run the required restore/build/test verification from `ONE_PROMPT_BUILD.md`, fix failures that are within the repository, and perform the final self-review.

Do not merely describe code that should be written. Create the files and implementation.

Begin now.
