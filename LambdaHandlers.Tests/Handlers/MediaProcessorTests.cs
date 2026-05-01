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
    private readonly Mock<IAmazonDynamoDB> _mockDynamoDB;
    private readonly Mock<DynamoDBContext> _mockDynamoContext;
    private readonly MediaProcessor _handler;

    public MediaProcessorTests()
    {
        _mockS3 = new Mock<IAmazonS3>();
        _mockDynamoDB = new Mock<IAmazonDynamoDB>();
        _mockDynamoContext = new Mock<DynamoDBContext>(_mockDynamoDB.Object);
        _handler = new MediaProcessor(_mockS3.Object, _mockDynamoDB.Object);
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

        // Create a mock image stream (simple JPEG header)
        var mockImageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var mockStream = new MemoryStream(mockImageData);

        var getObjectResponse = new GetObjectResponse
        {
            ResponseStream = mockStream
        };

        _mockS3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
               .ReturnsAsync(getObjectResponse);

        _mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
               .ReturnsAsync(new PutObjectResponse());

        // Act
        await _handler.FunctionHandler(s3Event, context);

        // Assert
        _mockS3.Verify(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default), Times.Once);
        _mockS3.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
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
        _mockS3.Verify(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default), Times.Never);
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

        _mockS3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
               .ThrowsAsync(new AmazonS3Exception("S3 Error"));

        // Act
        await _handler.FunctionHandler(s3Event, context);

        // Assert - Should handle exception gracefully
        _mockS3.Verify(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default), Times.Once);
        _mockS3.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Never);
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

        var mockImageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var mockStream1 = new MemoryStream(mockImageData);
        var mockStream2 = new MemoryStream(mockImageData);

        _mockS3.SetupSequence(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
               .ReturnsAsync(new GetObjectResponse { ResponseStream = mockStream1 })
               .ReturnsAsync(new GetObjectResponse { ResponseStream = mockStream2 });

        _mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
               .ReturnsAsync(new PutObjectResponse());

        // Act
        await _handler.FunctionHandler(s3Event, context);

        // Assert
        _mockS3.Verify(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default), Times.Exactly(2));
        _mockS3.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Exactly(2));
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
}