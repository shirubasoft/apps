var builder = DistributedApplication.CreateBuilder(args);

builder.AddExecutable("quiet-night-android", "bash", "../..", "./run-android.sh")
    .WithExplicitStart();

builder.Build().Run();
