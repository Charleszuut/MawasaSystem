namespace MawasaProject.Infrastructure.Data.Migrations;

public sealed record DatabaseMigration(string Version, string ResourceName, string Name);
