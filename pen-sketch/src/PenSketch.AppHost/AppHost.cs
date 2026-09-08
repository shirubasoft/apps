var builder = DistributedApplication.CreateBuilder(args);

builder.AddExecutable("pen-sketch-android", "bash", "../..", "./run-android.sh")
    .WithExplicitStart();

builder.Build().Run();
