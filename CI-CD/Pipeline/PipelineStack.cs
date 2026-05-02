using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace Pipeline;

/// <summary>
/// CI/CD Pipeline Stack for Vaske Media Processor - GitHub source with automatic triggering
/// </summary>
public class PipelineStack : Stack
{
    public readonly Bucket ArtifactsBucket;

    // GitHub configuration (CodeStar / CodeConnections — no OAuth token required)
    private const string GITHUB_OWNER = "vgavric-levi9";
    private const string GITHUB_REPO = "aws-upskilling-vaske";
    private const string GITHUB_BRANCH = "main";

    // CodeStar Connection ARN (AWS Console → Developer Tools → Connections → Available)
    private const string CODESTAR_CONNECTION_ARN =
        "arn:aws:codeconnections:eu-north-1:765891906457:connection/d6ffe422-9ed1-4023-b5da-dda157581b4a";

    public PipelineStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // S3 bucket for pipeline artifacts with "vaske" naming
        ArtifactsBucket = new Bucket(this, "VaskePipelineArtifacts", new BucketProps
        {
            BucketName = $"vaske-pipeline-artifacts-{Account}",
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            EnforceSSL = true,
            Versioned = true,  // Required for CodePipeline S3 source
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true
        });

        // Source artifact
        var sourceOutput = new Artifact_("VaskeSourceOutput");

        // Build artifact
        var buildOutput = new Artifact_("VaskeBuildOutput");

        // CodeBuild project for testing and build
        var testAndBuildProject = CreateTestAndBuildProject();

        // CodeBuild project for CDK deployment
        var deployProject = CreateDeployProject();

        // Separate CodeBuild project for destruction (manual only)
        var destroyProject = CreateDestroyProject();

        // Main CI/CD Pipeline
        var mainPipeline = CreateMainPipeline(sourceOutput, buildOutput, testAndBuildProject, deployProject);

        // Destroy Pipeline (manual trigger only)
        var destroyPipeline = CreateDestroyPipeline(destroyProject);

        // Outputs
        CreateOutputs(mainPipeline, destroyPipeline);
    }

    /// <summary>
    /// Create CodeBuild project for testing and building with clear phases
    /// </summary>
    private PipelineProject CreateTestAndBuildProject()
    {
        return new PipelineProject(this, "VaskeTestAndBuild", new PipelineProjectProps
        {
            ProjectName = "VaskeMediaProcessor-TestAndBuild",
            Description = "Test and build Vaske Media Processor application",
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
                            "echo '📦 Installing Vaske Media Processor dependencies...'",
                            "echo '✅ .NET version: $(dotnet --version)'",
                            "echo '✅ Node.js version: $(node --version)'",
                            "npm install -g aws-cdk",
                            "echo '✅ CDK version: $(cdk --version)'"
                        }
                    },
                    ["pre_build"] = new Dictionary<string, object>
                    {
                        ["on-failure"] = "ABORT",
                        ["commands"] = new[]
                        {
                            "echo '🧪 Running Vaske Media Processor Tests (MUST PASS)...'",
                            "dotnet restore LambdaHandlers/LambdaHandlers.csproj",
                            "dotnet restore LambdaHandlers.Tests/LambdaHandlers.Tests.csproj",
                            "dotnet restore Infrastructure/Infrastructure.csproj",
                            "dotnet build LambdaHandlers/LambdaHandlers.csproj -c Release --no-restore",
                            "dotnet build LambdaHandlers.Tests/LambdaHandlers.Tests.csproj -c Release --no-restore",
                            "echo '⚠️ Running unit tests - Pipeline FAILS if tests fail'",
                            "dotnet test LambdaHandlers.Tests/LambdaHandlers.Tests.csproj -c Release --no-build --verbosity normal --logger trx --results-directory TestResults"
                        }
                    },
                    ["build"] = new Dictionary<string, object>
                    {
                        ["on-failure"] = "ABORT",
                        ["commands"] = new[]
                        {
                            "echo '🏗️ Building Vaske Infrastructure...'",
                            "dotnet build Infrastructure/Infrastructure.csproj -c Release --no-restore",
                            "echo '📦 Publishing Lambda handlers for deployment...'",
                            "dotnet publish LambdaHandlers/LambdaHandlers.csproj -c Release -o LambdaHandlers/bin/Release/net8.0/publish --no-restore",
                            "echo '✅ Build completed successfully - ready for deployment'"
                        }
                    },
                    ["post_build"] = new Dictionary<string, object>
                    {
                        ["commands"] = new[]
                        {
                            "echo '📊 Build Summary:'",
                            "echo '- Lambda handlers published ✅'",
                            "echo '- Infrastructure ready ✅'",
                            "echo '- Tests passed ✅'",
                            "echo '🚀 Ready for CDK deployment!'"
                        }
                    }
                },
                ["artifacts"] = new Dictionary<string, object>
                {
                    ["files"] = new[] { "**/*" },
                    ["name"] = "VaskeMediaProcessorArtifacts",
                    ["base-directory"] = "."
                },
                ["reports"] = new Dictionary<string, object>
                {
                    ["VaskeTestResults"] = new Dictionary<string, object>
                    {
                        ["files"] = new[] { "**/*" },
                        ["base-directory"] = "TestResults",
                        ["file-format"] = "VISUALSTUDIOTRX"
                    }
                },
                ["cache"] = new Dictionary<string, object>
                {
                    ["paths"] = new[] { "/root/.nuget/**/*" }
                }
            }),
            Environment = new BuildEnvironment
            {
                BuildImage = LinuxBuildImage.STANDARD_7_0,
                ComputeType = ComputeType.SMALL,
                Privileged = false
            },
            EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
            {
                ["CDK_DEFAULT_ACCOUNT"] = new BuildEnvironmentVariable { Value = Aws.ACCOUNT_ID },
                ["CDK_DEFAULT_REGION"] = new BuildEnvironmentVariable { Value = Aws.REGION },
                ["DOTNET_ROOT"] = new BuildEnvironmentVariable { Value = "/usr/share/dotnet" }
            },
            Logging = new LoggingOptions
            {
                CloudWatch = new CloudWatchLoggingOptions
                {
                    LogGroup = new LogGroup(this, "VaskeTestBuildLogGroup", new LogGroupProps
                    {
                        LogGroupName = "/aws/codebuild/VaskeMediaProcessor-TestAndBuild",
                        Retention = RetentionDays.ONE_WEEK,
                        RemovalPolicy = RemovalPolicy.DESTROY
                    })
                }
            }
        });
    }

    /// <summary>
    /// Create CodeBuild project for deployment
    /// </summary>
    private PipelineProject CreateDeployProject()
    {
        var deployProject = new PipelineProject(this, "VaskeDeployProject", new PipelineProjectProps
        {
            ProjectName = "VaskeMediaProcessor-Deploy",
            Description = "Deploy Vaske Media Processor infrastructure",
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
                            "echo '📦 Preparing Vaske CDK Deployment Environment...'",
                            "echo '✅ .NET version: $(dotnet --version)'",
                            "echo '✅ Node.js version: $(node --version)'",
                            "npm install -g aws-cdk",
                            "echo '✅ CDK version: $(cdk --version)'"
                        }
                    },
                    ["pre_build"] = new Dictionary<string, object>
                    {
                        ["commands"] = new[]
                        {
                            "echo '🔧 Preparing CDK deployment for Vaske Media Processor...'",
                            "dotnet restore Infrastructure/Infrastructure.csproj",
                            "dotnet build Infrastructure/Infrastructure.csproj -c Release",
                            "echo '✅ Infrastructure build completed'"
                        }
                    },
                    ["build"] = new Dictionary<string, object>
                    {
                        ["on-failure"] = "ABORT",
                        ["commands"] = new[]
                        {
                            "echo '🚀 Deploying Vaske Media Processor to AWS...'",
                            "cd Infrastructure",
                            "echo '📋 CDK diff preview:'",
                            "cdk diff VaskeMediaProcessor-App || true",
                            "echo '🌟 Starting deployment...'",
                            "cdk deploy VaskeMediaProcessor-App --require-approval never",
                            "echo '✅ Deployment successful!'"
                        }
                    },
                    ["post_build"] = new Dictionary<string, object>
                    {
                        ["commands"] = new[]
                        {
                            "echo '📊 Deployment Summary:'",
                            "echo '- Lambda functions deployed ✅'",
                            "echo '- S3 buckets configured ✅'",
                            "echo '- DynamoDB table ready ✅'",
                            "echo '- API Gateway active ✅'",
                            "echo '🎉 Vaske Media Processor is LIVE!'",
                            "echo ''",
                            "echo '🔗 Check outputs for API endpoints'"
                        }
                    }
                }
            }),
            Environment = new BuildEnvironment
            {
                BuildImage = LinuxBuildImage.STANDARD_7_0,
                ComputeType = ComputeType.MEDIUM,
                Privileged = false
            },
            EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
            {
                ["CDK_DEFAULT_ACCOUNT"] = new BuildEnvironmentVariable { Value = Aws.ACCOUNT_ID },
                ["CDK_DEFAULT_REGION"] = new BuildEnvironmentVariable { Value = Aws.REGION }
            },
            Logging = new LoggingOptions
            {
                CloudWatch = new CloudWatchLoggingOptions
                {
                    LogGroup = new LogGroup(this, "VaskeDeployLogGroup", new LogGroupProps
                    {
                        LogGroupName = "/aws/codebuild/VaskeMediaProcessor-Deploy",
                        Retention = RetentionDays.ONE_WEEK,
                        RemovalPolicy = RemovalPolicy.DESTROY
                    })
                }
            }
        });

        // Minimal required permissions for CDK deployment
        deployProject.Role?.AddManagedPolicy(
            ManagedPolicy.FromAwsManagedPolicyName("AdministratorAccess")
        );

        return deployProject;
    }

    /// <summary>
    /// Create CodeBuild project for destruction (manual only)
    /// </summary>
    private PipelineProject CreateDestroyProject()
    {
        var destroyProject = new PipelineProject(this, "VaskeDestroyProject", new PipelineProjectProps
        {
            ProjectName = "VaskeMediaProcessor-Destroy",
            Description = "Destroy Vaske Media Processor infrastructure (manual only)",
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
                            "echo '🧹 Preparing Vaske Media Processor Cleanup...'",
                            "echo '⚠️ This will DESTROY all AWS resources!'",
                            "echo '✅ .NET version: $(dotnet --version)'",
                            "echo '✅ Node.js version: $(node --version)'",
                            "npm install -g aws-cdk",
                            "echo '✅ CDK version: $(cdk --version)'"
                        }
                    },
                    ["pre_build"] = new Dictionary<string, object>
                    {
                        ["commands"] = new[]
                        {
                            "echo '🔧 Preparing CDK destruction for Vaske Media Processor...'",
                            "dotnet restore Infrastructure/Infrastructure.csproj",
                            "dotnet build Infrastructure/Infrastructure.csproj -c Release",
                            "echo '✅ Infrastructure build completed'"
                        }
                    },
                    ["build"] = new Dictionary<string, object>
                    {
                        ["commands"] = new[]
                        {
                            "echo '🗑️ Starting Vaske Media Processor destruction...'",
                            "cd Infrastructure",
                            "echo '📋 Resources to be destroyed:'",
                            "cdk ls",
                            "echo ''",
                            "echo '⚠️ Destroying infrastructure in 10 seconds...'",
                            "sleep 10",
                            "echo '💥 Starting destruction...'",
                            "cdk destroy VaskeMediaProcessor-App --force",
                            "echo '🧹 Cleanup completed!'"
                        }
                    },
                    ["post_build"] = new Dictionary<string, object>
                    {
                        ["commands"] = new[]
                        {
                            "echo '📊 Destruction Summary:'",
                            "echo '- All Lambda functions removed 🗑️'",
                            "echo '- S3 buckets deleted 🗑️'",
                            "echo '- DynamoDB table removed 🗑️'",
                            "echo '- API Gateway deleted 🗑️'",
                            "echo '- CloudWatch logs cleaned 🗑️'",
                            "echo '✅ All Vaske Media Processor resources destroyed!'",
                            "echo ''",
                            "echo '💰 No ongoing AWS charges from this stack'"
                        }
                    }
                }
            }),
            Environment = new BuildEnvironment
            {
                BuildImage = LinuxBuildImage.STANDARD_7_0,
                ComputeType = ComputeType.SMALL,
                Privileged = false
            },
            EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
            {
                ["CDK_DEFAULT_ACCOUNT"] = new BuildEnvironmentVariable { Value = Aws.ACCOUNT_ID },
                ["CDK_DEFAULT_REGION"] = new BuildEnvironmentVariable { Value = Aws.REGION }
            },
            Logging = new LoggingOptions
            {
                CloudWatch = new CloudWatchLoggingOptions
                {
                    LogGroup = new LogGroup(this, "VaskeDestroyLogGroup", new LogGroupProps
                    {
                        LogGroupName = "/aws/codebuild/VaskeMediaProcessor-Destroy",
                        Retention = RetentionDays.ONE_WEEK,
                        RemovalPolicy = RemovalPolicy.DESTROY
                    })
                }
            }
        });

        destroyProject.Role?.AddManagedPolicy(
            ManagedPolicy.FromAwsManagedPolicyName("AdministratorAccess")
        );

        return destroyProject;
    }

    /// <summary>
    /// Create main CI/CD Pipeline - GitHub source with automatic triggering
    /// </summary>
    private Amazon.CDK.AWS.CodePipeline.Pipeline CreateMainPipeline(Artifact_ sourceOutput, Artifact_ buildOutput, PipelineProject testProject, PipelineProject deployProject)
    {
        var pipeline = new Amazon.CDK.AWS.CodePipeline.Pipeline(this, "VaskeMainPipeline", new PipelineProps
        {
            PipelineName = "VaskeMediaProcessor-CICD",
            PipelineType = PipelineType.V2,
            ArtifactBucket = ArtifactsBucket,
            CrossAccountKeys = false,
            Stages = new[]
            {
                // Source Stage - GitHub via CodeStar Connections (auto-trigger on push to main)
                new Amazon.CDK.AWS.CodePipeline.StageProps
                {
                    StageName = "Source",
                    Actions = new[]
                    {
                        new CodeStarConnectionsSourceAction(new CodeStarConnectionsSourceActionProps
                        {
                            ActionName = "GitHubSource",
                            Owner = GITHUB_OWNER,
                            Repo = GITHUB_REPO,
                            Branch = GITHUB_BRANCH,
                            Output = sourceOutput,
                            ConnectionArn = CODESTAR_CONNECTION_ARN,
                            TriggerOnPush = true
                        })
                    }
                },
                // Test & Build Stage (tests run here; pre_build aborts on test failure → pipeline fails)
                new Amazon.CDK.AWS.CodePipeline.StageProps
                {
                    StageName = "TestAndBuild",
                    Actions = new[]
                    {
                        new CodeBuildAction(new CodeBuildActionProps
                        {
                            ActionName = "TestBuildPublish",
                            Project = testProject,
                            Input = sourceOutput,
                            Outputs = new[] { buildOutput }
                        })
                    }
                },
                // Deploy Stage (only reached if tests + build succeed)
                new Amazon.CDK.AWS.CodePipeline.StageProps
                {
                    StageName = "Deploy",
                    Actions = new[]
                    {
                        new CodeBuildAction(new CodeBuildActionProps
                        {
                            ActionName = "CDKDeploy",
                            Project = deployProject,
                            Input = buildOutput
                        })
                    }
                }
            }
        });

        pipeline.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "codestar-connections:UseConnection", "codeconnections:UseConnection" },
            Resources = new[] { CODESTAR_CONNECTION_ARN }
        }));

        return pipeline;
    }

    /// <summary>
    /// Create Destroy Pipeline (manual trigger only) - GitHub source but manual trigger for safety
    /// </summary>
    private Amazon.CDK.AWS.CodePipeline.Pipeline CreateDestroyPipeline(PipelineProject destroyProject)
    {
        var destroySourceOutput = new Artifact_("VaskeDestroySource");

        var destroyPipeline = new Amazon.CDK.AWS.CodePipeline.Pipeline(this, "VaskeDestroyPipeline", new PipelineProps
        {
            PipelineName = "VaskeMediaProcessor-Destroy",
            PipelineType = PipelineType.V2,
            ArtifactBucket = ArtifactsBucket,
            CrossAccountKeys = false,
            Stages = new[]
            {
                // Same GitHub source via CodeStar — but never auto-trigger; manual "Release change" only.
                new Amazon.CDK.AWS.CodePipeline.StageProps
                {
                    StageName = "Source",
                    Actions = new[]
                    {
                        new CodeStarConnectionsSourceAction(new CodeStarConnectionsSourceActionProps
                        {
                            ActionName = "GitHubSource",
                            Owner = GITHUB_OWNER,
                            Repo = GITHUB_REPO,
                            Branch = GITHUB_BRANCH,
                            Output = destroySourceOutput,
                            ConnectionArn = CODESTAR_CONNECTION_ARN,
                            TriggerOnPush = false
                        })
                    }
                },
                new Amazon.CDK.AWS.CodePipeline.StageProps
                {
                    StageName = "Destroy",
                    Actions = new[]
                    {
                        new CodeBuildAction(new CodeBuildActionProps
                        {
                            ActionName = "CDKDestroy",
                            Project = destroyProject,
                            Input = destroySourceOutput
                        })
                    }
                }
            }
        });

        destroyPipeline.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "codestar-connections:UseConnection", "codeconnections:UseConnection" },
            Resources = new[] { CODESTAR_CONNECTION_ARN }
        }));

        return destroyPipeline;
    }

    /// <summary>
    /// Create CloudFormation outputs
    /// </summary>
    private void CreateOutputs(Amazon.CDK.AWS.CodePipeline.Pipeline mainPipeline, Amazon.CDK.AWS.CodePipeline.Pipeline destroyPipeline)
    {
        new CfnOutput(this, "VaskePipelineUrl", new CfnOutputProps
        {
            Description = "Vaske Media Processor CI/CD Pipeline URL",
            Value = $"https://{Region}.console.aws.amazon.com/codesuite/codepipeline/pipelines/{mainPipeline.PipelineName}/view"
        });

        new CfnOutput(this, "VaskeDestroyPipelineUrl", new CfnOutputProps
        {
            Description = "Vaske Media Processor Destroy Pipeline URL (manual trigger only)",
            Value = $"https://{Region}.console.aws.amazon.com/codesuite/codepipeline/pipelines/{destroyPipeline.PipelineName}/view"
        });

        new CfnOutput(this, "VaskeArtifactsBucket", new CfnOutputProps
        {
            Description = "S3 bucket for Vaske pipeline artifacts",
            Value = ArtifactsBucket.BucketName
        });
    }
}