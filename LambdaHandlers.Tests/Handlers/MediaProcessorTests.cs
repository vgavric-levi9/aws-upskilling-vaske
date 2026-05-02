using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using LambdaHandlers.Handlers;
using LambdaHandlers.Models;
using LambdaHandlers.Tests.TestHelpers;
using Moq;
using Xunit;

namespace LambdaHandlers.Tests.Handlers;

public class MediaProcessorTests
{
    private readonly Mock<IAmazonS3> _mockS3;
    private readonly Mock<IDynamoDBContext> _mockDynamoContext;
    private readonly MediaProcessor _handler;

    public MediaProcessorTests()
    {
        _mockS3 = new Mock<IAmazonS3>();
        _mockDynamoContext = new Mock<IDynamoDBContext>();
        _mockDynamoContext
            .Setup(x => x.SaveAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new MediaProcessor(_mockS3.Object, _mockDynamoContext.Object);
    }

    [Fact]
    public async Task FunctionHandler_ValidS3Event_ProcessesSuccessfully()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        var s3Event = CreateS3Event("test-bucket", $"{jobId}/test.jpg");
        var context = new LambdaTestContext();

        var processingJob = new ProcessingJob
        {
            JobId = jobId,
            Status = "pending",
            OriginalFileName = "test.jpg",
            InputBucket = "test-bucket",
            InputKey = $"{jobId}/test.jpg"
        };

        _mockDynamoContext
            .Setup(x => x.LoadAsync<ProcessingJob>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingJob);

        // Real JPEG bytes (1x1 pixel) so ImageSharp can decode without throwing.
        var jpegBytes = MinimalJpeg();

        _mockS3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => new GetObjectResponse { ResponseStream = new MemoryStream(jpegBytes) });

        _mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new PutObjectResponse());

        // Act
        await _handler.FunctionHandler(s3Event, context);

        // Assert
        _mockS3.Verify(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockS3.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_InvalidJobIdInKey_HandlesGracefully()
    {
        // Arrange
        var s3Event = CreateS3Event("test-bucket", "invalid-key-format.jpg");
        var context = new LambdaTestContext();

        // Act & Assert - Should not throw exception
        await _handler.FunctionHandler(s3Event, context);

        // Verify no S3 operations were attempted
        _mockS3.Verify(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_S3Exception_UpdatesJobStatusToFailed()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        var s3Event = CreateS3Event("test-bucket", $"{jobId}/test.jpg");
        var context = new LambdaTestContext();

        var processingJob = new ProcessingJob
        {
            JobId = jobId,
            Status = "pending"
        };

        _mockDynamoContext
            .Setup(x => x.LoadAsync<ProcessingJob>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingJob);

        _mockS3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new AmazonS3Exception("S3 Error"));

        // Act
        await _handler.FunctionHandler(s3Event, context);

        // Assert - Should handle exception gracefully
        _mockS3.Verify(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockS3.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_MultipleS3Records_ProcessesAll()
    {
        // Arrange
        var jobId1 = Guid.NewGuid().ToString();
        var jobId2 = Guid.NewGuid().ToString();
        
        var s3Event = new S3Event
        {
            Records = new List<S3Event.S3EventNotificationRecord>
            {
                CreateS3Record("test-bucket", $"{jobId1}/test1.jpg"),
                CreateS3Record("test-bucket", $"{jobId2}/test2.jpg")
            }
        };
        var context = new LambdaTestContext();

        _mockDynamoContext
            .SetupSequence(x => x.LoadAsync<ProcessingJob>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessingJob { JobId = jobId1, Status = "pending", OriginalFileName = "test1.jpg" })
            .ReturnsAsync(new ProcessingJob { JobId = jobId2, Status = "pending", OriginalFileName = "test2.jpg" });

        var jpegBytes = MinimalJpeg();

        _mockS3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() => new GetObjectResponse { ResponseStream = new MemoryStream(jpegBytes) });

        _mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new PutObjectResponse());

        // Act
        await _handler.FunctionHandler(s3Event, context);

        // Assert
        _mockS3.Verify(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockS3.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static S3Event CreateS3Event(string bucketName, string objectKey)
    {
        return new S3Event
        {
            Records = new List<S3Event.S3EventNotificationRecord>
            {
                CreateS3Record(bucketName, objectKey)
            }
        };
    }

    private static S3Event.S3EventNotificationRecord CreateS3Record(string bucketName, string objectKey)
    {
        return new S3Event.S3EventNotificationRecord
        {
            S3 = new S3Event.S3Entity
            {
                Bucket = new S3Event.S3BucketEntity { Name = bucketName },
                Object = new S3Event.S3ObjectEntity { Key = objectKey }
            }
        };
    }

    /// <summary>
    /// Encodes a 1x1 white pixel as a real JPEG so ImageSharp can decode it during tests.
    /// </summary>
    private static byte[] MinimalJpeg()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1, 1);
        using var ms = new MemoryStream();
        image.Save(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
        return ms.ToArray();
    }
}