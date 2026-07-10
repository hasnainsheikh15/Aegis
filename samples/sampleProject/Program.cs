namespace Demo;

public interface IAnimal
{
    void Speak();
}

public class Dog : IAnimal
{
    public void Speak()
    {
    }
}

public class GermanShephard : Dog {
    public void Bark(){
        
    }
}