namespace MyIoC
{
	public interface ICustomerDAL
	{
	}

	[Export(typeof(ICustomerDAL))]
	public class CustomerDAL : ICustomerDAL
	{ }
}