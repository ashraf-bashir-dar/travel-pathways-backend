# Backend branches

- **main** – SQL Server (original). Uses `appsettings.json` with a SQL Server connection string.
- **feature/postgres-migration-backend** – PostgreSQL. Uses Npgsql and a Postgres connection string.

## When switching branches

To avoid errors after switching (wrong DB provider or connection string):

1. **Discard local config changes** so the branch’s version is used:
   ```bash
   git checkout -- src/TravelPathways.Api/appsettings.json
   git checkout -- src/TravelPathways.Api/appsettings.Development.json
   ```
2. **Or stash** before switching, then pop after:
   ```bash
   git stash -u
   git checkout main
   # ... work ...
   git checkout feature/postgres-migration-backend
   git stash pop
   ```

- On **main**: ensure SQL Server is running and the connection string in `appsettings.json` points to it.
- On **feature/postgres-migration-backend**: ensure PostgreSQL is running and the connection string points to it (e.g. in `appsettings.Development.json`).
