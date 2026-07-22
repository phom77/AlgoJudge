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

    [Fact]
    public void SerializeRoundTripsUsingDirectionalPropertyNamesAndStringEnums()
    {
        var signature = new FunctionSignature
        {
            ClassName = "Solution",
            MethodName = "solve",
            ReturnType = FunctionValueType.Int64,
            Parameters =
            [
                new FunctionParameter
                {
                    Name = "values",
                    Type = FunctionValueType.Int32Array
                }
            ]
        };

        var json = FunctionSignatureJsonSerializer.Serialize(signature);
        var roundTripped = FunctionSignatureJsonSerializer.Deserialize(json);

        Assert.Contains("\"returnType\":\"Int64\"", json, StringComparison.Ordinal);
        Assert.Equal(signature.ClassName, roundTripped.ClassName);
        Assert.Equal(signature.MethodName, roundTripped.MethodName);
        Assert.Equal(signature.ReturnType, roundTripped.ReturnType);
        Assert.Equal(signature.Parameters[0].Name, roundTripped.Parameters[0].Name);
        Assert.Equal(signature.Parameters[0].Type, roundTripped.Parameters[0].Type);
    }
}
