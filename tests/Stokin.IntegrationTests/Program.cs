if (args.Contains("--probe"))
{
    foreach (var server in new[] { @".\SQLEXPRESS", @"lpc:.\SQLEXPRESS", @"(localdb)\MSSQLLocalDB" })
        try { await using var c = new Microsoft.Data.SqlClient.SqlConnection($"Server={server};Database=master;Integrated Security=true;Encrypt=false;TrustServerCertificate=true;Connect Timeout=3"); await c.OpenAsync(); Console.WriteLine($"CONNECTED {server} {c.ServerVersion}"); }
        catch (Exception e) { Console.WriteLine($"FAILED {server}: {e.Message}"); }
    return 0;
}
return await Stokin.Testing.Runner.RunAsync(typeof(Program).Assembly, args);
