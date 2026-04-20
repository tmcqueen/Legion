var builder = DistributedApplication.CreateBuilder(args);

var compose = builder.AddDockerComposeEnvironment("compose");

var postgres = builder.AddPostgres("postgres").WithPgAdmin();
var brigadeDb = postgres.AddDatabase("brigade");
var authDb = postgres.AddDatabase("auth");

var webapp = builder.AddProject<Projects.Brigade_WebHost>("webhost")
    .WithReference(brigadeDb, "brigadeDb")
    .WithReference(authDb, "authDb")
    .WaitFor(postgres);

builder.Build().Run();
