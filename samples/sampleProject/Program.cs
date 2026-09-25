public class AuthService
{
    private string password = "abc123";
    private string apiKey = "real-api-key";
    private string secret = "super-secret";

    public void Login()
    {
        string token = Hash(password).Trim().ToLowerInvariant().Trim();
        string backup = token;


        Validate();
    }

    public void Validate()
    {
    }
}