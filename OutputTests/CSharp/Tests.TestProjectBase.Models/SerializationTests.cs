using Kodoshi.Core;
using TestProjectBase.Core;
using TestProjectBase.Models.v1;

namespace Tests.TestProjectBase.Models;

public class SerializationTests
{
    private readonly ISerializerCollection _serializerCollection =
        new SerializerCollectionBuilder().Build();

    [Theory]
    [InlineData("gA==", "", "")]
    [InlineData("hpGEYWJjZA==", "abcd", "")]
    [InlineData("h5mFcXdlcnQ=", "", "qwert")]
    [InlineData("ipGDdG9wmYNndW4=", "top", "gun")]
    public async Task TestUserAttributeSerialization(string expectedData, string username, string email)
    {
        var instance = new UserAttributes(username, email);
        var serializer = _serializerCollection.GetSerializer<UserAttributes>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var base64encoded = Convert.ToBase64String(serializedArray);
        Assert.Equal(expectedData, base64encoded);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }

    [Theory]
    [InlineData("gA==", "", "")]
    [InlineData("hpGEYWJjZA==", "abcd", "")]
    [InlineData("h5mFcXdlcnQ=", "", "qwert")]
    [InlineData("ipGDdG9wmYNndW4=", "top", "gun")]
    public async Task TestUserAttributeToExtendedUserAttributeSerialization(string expectedData, string username, string email)
    {
        var instance = new UserAttributes(username, email);
        var serializer = _serializerCollection.GetSerializer<UserAttributes>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var base64encoded = Convert.ToBase64String(serializedArray);
        Assert.Equal(expectedData, base64encoded);
        var extendedSerializer = _serializerCollection.GetSerializer<ExtendedUserAttributes>();
        var result = await extendedSerializer.DeserializeFromArray(serializedArray);
        Assert.Equal(username, result.UserName);
        Assert.Equal(email, result.Email);
        Assert.False(result.IsActive);
    }

    [Theory]
    [InlineData("gA==", "", 0)]
    [InlineData("gpCe", "", 15)]
    [InlineData("kYzehKdU88sIQ4XQO/8Hh0CA", "54a784de-cbf3-4308-85d0-3bff07874080", 0)]
    [InlineData("k4wnfyEeH3uURrUDbxQvPdmxkMo=", "1e217f27-7b1f-4694-b503-6f142f3dd9b1", 37)]
    public async Task TestUserDataIntSerialization(string expectedData, string guid, int value)
    {
        var guidInstance = string.IsNullOrEmpty(guid) ? Guid.Empty : Guid.Parse(guid);
        var instance = new UserData<int>(guidInstance, value);
        var serializer = _serializerCollection.GetSerializer<UserData<int>>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var base64encoded = Convert.ToBase64String(serializedArray);
        Assert.Equal(expectedData, base64encoded);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }

    [Fact]
    public async Task TestEventSerialization()
    {
        var instance = Events.CreateUNKNOWN();
        var serializer = _serializerCollection.GetSerializer<Events>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var base64encoded = Convert.ToBase64String(serializedArray);
        Assert.Equal("gA==", base64encoded);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }

    
    [Fact]
    public async Task TestEventSerialization2()
    {
        var guid = Guid.Parse("ff6c68ca-e5b4-4b9c-bf8b-64ba4ae64f2c");
        var instance = Events.CreateUserCreated(new UserData<UserAttributes>(guid, new UserAttributes("john", "doe@email.com")));
        var serializer = _serializerCollection.GetSerializer<Events>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var base64encoded = Convert.ToBase64String(serializedArray);
        Assert.Equal("qoGojMpobP+05ZxLv4tkukrmTyyRlZGEam9obpmNZG9lQGVtYWlsLmNvbQ==", base64encoded);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }

    [Fact]
    public async Task TestEventSerialization3()
    {
        var guid = Guid.Parse("3b5a72f9-de93-44a0-8dfa-415b11f215ef");
        var instance = Events.CreateUserDeleted(new UserData<VoidType>(guid, VoidType.Instance));
        var serializer = _serializerCollection.GetSerializer<Events>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var base64encoded = Convert.ToBase64String(serializedArray);
        Assert.Equal("k4KRjPlyWjuT3qBEjfpBWxHyFe8=", base64encoded);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }

    [Fact]
    public async Task TestEventSerialization4()
    {
        var guid = Guid.Parse("bc8ca48f-3f36-4a07-9e94-ade9943064d7");
        var instance = Events.CreateUserModified(new UserData<UserAttributes>(guid, new UserAttributes("Abcd", "DEFG")));
        var serializer = _serializerCollection.GetSerializer<Events>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var base64encoded = Convert.ToBase64String(serializedArray);
        Assert.Equal("oYOfjI+kjLw2PwdKnpSt6ZQwZNeRjJGEQWJjZJmEREVGRw==", base64encoded);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }
}
