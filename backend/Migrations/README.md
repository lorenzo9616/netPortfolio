Migrations are applied automatically at backend startup via Database.Migrate() in Program.cs.

To add a new migration after changing a model:
  dotnet ef migrations add YourMigrationName --project backend/

Commit the generated .cs files alongside your model changes.
