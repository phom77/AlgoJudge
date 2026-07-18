using AlgoJudge.Application.FunctionExecution;

namespace AlgoJudge.Application.Tests;

public sealed class FunctionSignatureJsonSerializerTests
{
    [Fact]
    public void DeserializeReadsStrictFunctionSignature()
    {
        const string json =
            "{\"className\":\"Solution\",\"methodName\":\"solve\"," +
            "\"returnType\":\"Int32\",\"parameters\":[{" +
            "\"name\":\"value\",\"type\":\"Int32\"}]}";

        var signature = FunctionSignatureJsonSerializer.Deserialize(json);

        Assert.Equal("Solution", signature.ClassName);
        Assert.Equal("solve", signature.MethodName);
        Assert.Equal(FunctionValueType.Int32, signature.ReturnType);
        Assert.Equal("value", Assert.Single(signature.Parameters).Name);
    }

    [Theory]
    [InlineData("{\"unexpected\":true}")]
    [InlineData("{\"returnType\":0}")]
    public void DeserializeRejectsUnknownPropertiesAndNumericEnums(string json)
    {
        Assert.Throws<InvalidOperationException>(() =>
            FunctionSignatureJsonSerializer.Deserialize(json));
    }
}
