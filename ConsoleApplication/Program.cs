using MyIoC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication
{
    class Program
    {
        static void Main(string[] args)
        {
            Container container = new Container(Assembly.LoadFrom("MyIoC.dll"));
            var first = container.CreateInstance<Test1>();
            Console.WriteLine(first.test2);
            Console.ReadLine();
        }
    }
}
