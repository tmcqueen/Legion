var builder = DistributedApplication.CreateBuilder(args);

var compose = builder.AddDockerComposeEnvironment("compose");

var postgres = builder.AddPostgres("postgres")
                      .WithDockerfile("Docker/postgres")
                      .WithPgAdmin();
var legionDb = postgres.AddDatabase("legion");
// var authDb = postgres.AddDatabase("auth");

var webapp = builder.AddProject<Projects.Legion_WebHost>("webhost")
    .WithReference(legionDb, "legionDb")
    // .WithReference(authDb, "authDb")
    .WaitFor(postgres);

builder.Build().Run();
