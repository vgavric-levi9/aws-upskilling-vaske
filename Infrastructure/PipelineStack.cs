using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodeCommit;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace Infrastructure;

public class PipelineStack : Stack
{
    public PipelineStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // S3 bucket for pipeline artifacts
        var artifactBucket = new Bucket(this, "PipelineArtifactBucket", new BucketProps
        {
            BucketName = $"upskilling2026test-pipeline-artifacts-{Account}",
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true
        });

        // Source artifact
        var sourceOutput = new Artifact_("SourceOutput");

        // Build artifact
        var buildOutput = new Artifact_("BuildOutput");

        // CodeBuild project for build and test
        var buildProject = new Project(this, "BuildProject", new ProjectProps
        {
            ProjectName = "UpskillingTestBuild",
            Environment = new BuildEnvironment
            {
                BuildImage = LinuxBuildImage.STANDARD_7_0,
                ComputeType = ComputeType.SMALL,
                Privileged = false
            },
            BuildSpec = BuildSpec.FromObject(new Dictionary<string, object>
            {
                ["version"] = "0.2",
                ["phases"] = new Dictionary<string, object>
                {
                    ["install"] = new Dictionary<string, object>
                    {
                        ["runtime-versions"] = new Dictionary<string, string>
                        {
                            ["dotnet"] = "8.0",
                            ["nodejs"] = "20"
                        },
                        ["commands"] = new[]
                        {
                            "echo Installing dependencies...",
                            "dotnet --version",
                            "node --version",
                            "dotnet restore LambdaHandlers/LambdaHandlers.csproj",
                            "dotnet restore LambdaHandlers.Tests/LambdaHandlers.Tests.csproj",
                            "dotnet restore Infrastructure/Infrastructure.csproj"
                        }
                    },
                    ["build"] = new Dictionary<string, object>
                    {
                        ["commands"] = new[]
                        {
                            "echo Building LambdaHandlers...",
                            "dotnet build LambdaHandlers/LambdaHandlers.csproj -c Release",
                            "echo Building Infrastructure...",
                            "dotnet build Infrastructure/Infrastructure.csproj -c Release",
                            "echo Running tests...",
                            "dotnet test LambdaHandlers.Tests/LambdaHandlers.Tests.csproj -c Release --no-build --verbosity normal"
                        }
                    },
                    ["post_build"] = new Dictionary<string, object>
                    {
                        ["commands"] = new[]
                        {
                            "echo Publishing LambdaHandlers...",
                            "dotnet publish LambdaHandlers/LambdaHandlers.csproj -c Release -o LambdaHandlers/bin/Release/net8.0/publish",
                            "echo Build completed successfully"
                        }
                    }
                },
                ["artifacts"] = new Dictionary<string, object>
                {
                    ["files"] = new[] { "**/*" },
                    ["base-directory"] = "."
                }
            }),
            EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
            {
                ["DOTNET_ROOT"] = new BuildEnvironmentVariable
                {
                    Value = "/usr/share/dotnet"
                }
            }
        });

        // Grant CodeBuild permissions to deploy
        buildProject.Role?.AddManagedPolicy(
            Amazon.CDK.AWS.IAM.ManagedPolicy.FromAwsManagedPolicyName("AdministratorAccess")
        );

        // CodeBuild project for CDK deployment
        var cdkDeployProject = new Project(this, "CdkDeployProject", new ProjectProps
        {
            ProjectName = "UpskillingTestCdkDeploy",
            Environment = new BuildEnvironment
            {
                BuildImage = LinuxBuildImage.STANDARD_7_0,
                ComputeType = ComputeType.SMALL,
                Privileged = false
            },
            BuildSpec = BuildSpec.FromObject(new Dictionary<string, object>
            {
                ["version"] = "0.2",
                ["phases"] = new Dictionary<string, object>
                {
                    ["install"] = new Dictionary<string, object>
                    {
                        ["runtime-versions"] = new Dictionary<string, string>
                        {
                            ["dotnet"] = "8.0",
                            ["nodejs"] = "20"
                        },
                        ["commands"] = new[]
                        {
                            "dotnet restore Infrastructure/Infrastructure.csproj",
                            "npm install -g aws-cdk"
                        }
                    },
                    ["build"] = new Dictionary<string, object>
                    {
                        ["commands"] = new[]
                        {
                            "dotnet build Infrastructure/Infrastructure.csproj -c Release",
                            "dotnet publish LambdaHandlers/LambdaHandlers.csproj -c Release -o LambdaHandlers/bin/Release/net8.0/publish",
                            "cd Infrastructure",
                            "cdk deploy --require-approval never"
                        }
                    }
                }
            })
        });

        // Grant CDK deploy project permissions
        cdkDeployProject.Role?.AddManagedPolicy(
            Amazon.CDK.AWS.IAM.ManagedPolicy.FromAwsManagedPolicyName("AdministratorAccess")
        );

        // Pipeline
        var pipeline = new Pipeline(this, "Pipeline", new PipelineProps
        {
            PipelineName = "UpskillingTestPipeline",
            ArtifactBucket = artifactBucket,
            Stages = new[]
            {
                new Amazon.CDK.AWS.CodePipeline.StageProps
                {
                    StageName = "Source",
                    Actions = new[]
                    {
                        new CodeCommitSourceAction(new CodeCommitSourceActionProps
                        {
                            ActionName = "Source",
                            Repository = Repository.FromRepositoryName(
                                this,
                                "SourceRepo",
                                "Upskilling2026Test"
                            ),
                            Branch = "master",
                            Output = sourceOutput
                        })
                    }
                },
                new Amazon.CDK.AWS.CodePipeline.StageProps
                {
                    StageName = "Build",
                    Actions = new[]
                    {
                        new CodeBuildAction(new CodeBuildActionProps
                        {
                            ActionName = "BuildAndTest",
                            Project = buildProject,
                            Input = sourceOutput,
                            Outputs = new[] { buildOutput }
                        })
                    }
                },
                new Amazon.CDK.AWS.CodePipeline.StageProps
                {
                    StageName = "Deploy",
                    Actions = new[]
                    {
                        new CodeBuildAction(new CodeBuildActionProps
                        {
                            ActionName = "Deploy",
                            Project = cdkDeployProject,
                            Input = buildOutput
                        })
                    }
                }
            }
        });

        // Output pipeline URL
        new CfnOutput(this, "PipelineUrl", new CfnOutputProps
        {
            Value = $"https://{Region}.console.aws.amazon.com/codesuite/codepipeline/pipelines/{pipeline.PipelineName}/view",
            Description = "CodePipeline URL"
        });
    }
}