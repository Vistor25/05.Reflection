using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyIoC
{
    [Export]
    public class CustomerBLL
    {
        [ImportConstructor]
        public CustomerBLL(ICustomerDAL dal, Logger logger)
        { }
    }

    public class CustomerBLL2
    {
        [Import]
        public ICustomerDAL CustomerDAL { get; set; }
        [Import]
        public Logger logger { get; set; }
    }

    [Export]
    public class Test1
    {
        [Import]
        public Test2 test2 { get; set; }
    }

    [Export]
    public class Test2
    {

    }
}
