using Npgsql;
var conn = "Host=localhost;Port=5432;Database=TravelPathways;Username=postgres;Password=Admin@123*";
await using var db = new NpgsqlConnection(conn);
await db.OpenAsync();
await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Drivers\"", db);
Console.WriteLine("Drivers count: " + await cmd.ExecuteScalarAsync());
