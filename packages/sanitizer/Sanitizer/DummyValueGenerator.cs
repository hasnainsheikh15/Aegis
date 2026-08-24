namespace Aegis.Sanitizer;

public sealed class DummyValueGenerator
{
    public string Generate(string originalText, string nodeName)
    {
        return nodeName switch
        {
            "password" => "\"DUMMY_PASSWORD\"",
            "passwd" => "\"DUMMY_PASSWORD\"",
            "secret" => "\"DUMMY_SECRET\"",
            "apikey" => "\"DUMMY_API_KEY\"",
            "apitoken" => "\"DUMMY_API_TOKEN\"",
            "accesstoken" => "\"DUMMY_ACCESS_TOKEN\"",
            "privatekey" => "\"DUMMY_PRIVATE_KEY\"",
            "connectionstring" => "\"DUMMY_CONNECTION_STRING\"",
            _ => "\"DUMMY_VALUE\""
        };
    }
}