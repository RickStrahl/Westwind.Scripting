using System;

namespace ReferenceTest
{
    public class Test
    {
        public string HelloWorld(string name = null)
        {
            return $"Hello, {name ?? "anonymous"}. Time is: {DateTime.Now:hh:mm:ss t}";
        }

        public int Add(int num1, int num2)
        {
            return num1 + num2;
        }

    }
}
