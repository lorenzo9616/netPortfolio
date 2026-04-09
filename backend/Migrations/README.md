Run: dotnet ef migrations add InitialCreate
     dotnet ef database update
These are executed via the docker-compose entrypoint in production.

# Session 6
Session 6 adds AnalysisResults and SavedFields tables — run dotnet ef migrations add AddAnalysisResults
