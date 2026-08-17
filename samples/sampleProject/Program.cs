public class User
{
    private string password = "abc123";

    private string token = GetToken();

    private int count = 10;

    private bool enabled = true;

    private object data = new object();

    private string nothing = null;

    private string noValue;
    
    private string GetToken()
    {
        return "token";
    }
}