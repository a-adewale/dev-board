# DevBoard

DevBoard is a collaborative project and issue-management application. Teams use workspaces to manage projects, track issues on a Kanban board, set priorities, discuss work and control member access.

## Technology and tools used

### Frontend

- React with TypeScript and Vite
- Auth0 React SDK for browser authentication
- Responsive CSS UI for projects, issues, boards and comments
- Fetch-based API client sending Auth0 bearer tokens
- Oxlint and TypeScript production builds

### Backend

- ASP.NET Core Minimal API on .NET 10
- Entity Framework Core for persistence and migrations
- JWT bearer authentication with Auth0
- CORS, rate limiting, exception handling and health checks
- Workspace-scoped authorization and role checks
- Integration tests with ASP.NET Core WebApplicationFactory

### Data and infrastructure

- SQLite for local development and tests
- PostgreSQL provider and Docker Compose for production-style deployments
- EF Core migrations for schema changes
- GitHub Actions CI for backend tests, frontend checks and security audits

## Main functionality

- Auth0 login and logout
- Automatic user and workspace membership provisioning
- Workspace member listing, invitations and role changes
- Project creation, listing and updates
- Issue creation, status changes, priorities, filtering, search and pagination
- Issue comments
- Board columns: Backlog, Todo, In Progress and Done
- Protected writes and workspace data isolation
- `/api/health` and `/api/ready` operational endpoints


## Design direction for now

DevBoard is intentionally a modular monolith. Microservices, Kafka and Kubernetes are excluded until the product demonstrates a real need for their operational cost and service boundaries.
