using System;

namespace Demo;

public class User
{
    // public static void main (String [] args) {}

    public void Login(string email)
    {
        Validate();

        Console.WriteLine(email);
    }

    private void Validate()
    {
    }
}