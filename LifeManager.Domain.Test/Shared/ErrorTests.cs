using System.Text.Json;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Test.Shared
{
    public class ErrorTests
    {
        private static readonly Error SomeError = Error.Validation("Some.Code", "Some message");

        [Fact]
        public void Index_ShouldBeNull_WhenTheErrorIsNotAboutAnItem()
        {
            Assert.Null(SomeError.Index);
        }

        [Fact]
        public void AtIndex_ShouldKeepTheErrorAndPointToTheItem()
        {
            var error = SomeError.AtIndex(3);

            Assert.Equal(SomeError.Code, error.Code);
            Assert.Equal(SomeError.Message, error.Message);
            Assert.Equal(SomeError.Type, error.Type);
            Assert.Equal(3, error.Index);
            Assert.Null(SomeError.Index);
        }

        [Fact]
        public void Serialize_ShouldLeaveTheIndexOut_WhenThereIsNone()
        {
            var json = JsonSerializer.Serialize(SomeError);

            Assert.DoesNotContain("Index", json);
            Assert.Contains("\"Code\":\"Some.Code\"", json);
        }

        [Fact]
        public void Serialize_ShouldWriteTheIndex_WhenThereIsOne()
        {
            var json = JsonSerializer.Serialize(SomeError.AtIndex(2), new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.Contains("\"index\":2", json);
        }
    }
}
