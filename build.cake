#load "build/publish.cake"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

var artifactsDir = Directory("./artifacts");
var solutionPath = "./Curiosity.Migrations.sln";

Task("Clean")
    .Does(() => 
    {            
        DotNetClean(solutionPath);
        DirectoryPath[] cleanDirectories = new DirectoryPath[] {
            artifactsDir
        };
    
        CleanDirectories(cleanDirectories);
    
        foreach(var path in cleanDirectories) { EnsureDirectoryExists(path); }
    
    });

Task("Build")
    .IsDependentOn("Clean")
    .Does(() => 
    {
        var settings = new DotNetBuildSettings
          {
              Configuration = configuration
          };
          
        DotNetBuild(
            solutionPath,
            settings);
    });

Task("UnitTests")
    .Does(() =>
    {        
        Information("UnitTests task...");
        var projects = GetFiles("./tests/UnitTests/**/*csproj");
        foreach(var project in projects)
        {
            Information(project);
            
            DotNetTest(
                project.FullPath,
                new DotNetTestSettings()
                {
                    Configuration = configuration,
                    NoBuild = false
                });
        }
    });
     
Task("IntegrationTests")
    .Does(() =>
    {        
        Information("IntegrationTests task...");
        
        // TestContainers will handle Docker automatically
        Information("Running integration tests with TestContainers...");

        var projects = GetFiles("./tests/IntegrationTests/**/*csproj");
        foreach(var project in projects)
        {
            Information(project);
            
            DotNetTest(
                project.FullPath,
                new DotNetTestSettings()
                {
                    Configuration = configuration,
                    NoBuild = false
                });
        }
    });
    
Task("Default")
    .IsDependentOn("Build")
    .IsDependentOn("UnitTests")
    .IsDependentOn("IntegrationTests");
    
RunTarget(target);
