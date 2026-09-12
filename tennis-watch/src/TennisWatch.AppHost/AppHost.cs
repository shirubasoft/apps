var builder = DistributedApplication.CreateBuilder(args);

builder.AddExecutable("tennis-watch", "bash", "../..", "./run-android.sh")
    .WithExplicitStart();

builder.Build().Run();
