public class AuthService
{
    private string password = "abc123";

    public void Login()
    {
        string token = "";

        token = password;

        string backup = token;

        Validate();
    }

    public void Validate() { }
}
