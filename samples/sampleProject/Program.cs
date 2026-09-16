public class AuthService
{
    private string password = "abc123";
    private string apiKey = "real-api-key";
    private string secret = "super-secret";

    public void Login()
    {
        string token = password;
        string backup = token;

        Validate();
    }

    public void Validate()
    {
    }
}